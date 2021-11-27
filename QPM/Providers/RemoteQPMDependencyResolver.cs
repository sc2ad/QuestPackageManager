using LibGit2Sharp;
using QPM.Commands;
using QPM.Data;
using QPM.Providers;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM
{
    internal class RemoteQPMDependencyResolver : IDependencyResolver
    {
        private readonly HttpClient client;
        private readonly QPMApi api;
        private readonly Dictionary<RestoredDependencyPair, SharedConfig> cached = new();
        private readonly AndroidMkProvider androidMkProvider;

        public RemoteQPMDependencyResolver(QPMApi api, AndroidMkProvider mkProvider)
        {
            client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Program.Config.DependencyTimeoutSeconds)
            };
            client.DefaultRequestHeaders.Add("User-Agent", "QPM_" + Assembly.GetCallingAssembly().GetName().Version?.ToString());
            this.api = api;
            androidMkProvider = mkProvider;
        }

        private const string DownloadGithubUrl = "https://github.com";
        private const string DefaultBranch = "master";

        private bool _gotMk = false;
        private AndroidMk? _cachedMk;

        private AndroidMk? GetMk()
        {
            if (!_gotMk)
            {
                _cachedMk = androidMkProvider.GetFile();
                _gotMk = true;
            }
            return _cachedMk;
        }

        internal void OnRestore(RestoreHandler self, Dictionary<RestoredDependencyPair, SharedConfig> deps, Dictionary<RestoredDependencyPair, SharedConfig> uniques) => Complete();

        internal void Complete()
        {
            // Write Android.mk file
            var val = GetMk();
            if (val != null)
                androidMkProvider.SerializeFile(val);
        }

        private bool IsGithubLink(Uri uri) => uri.AbsoluteUri.StartsWith(DownloadGithubUrl);

        private bool DependencyCached(string downloadFolder, in SharedConfig dependencyConfig)
        {
            if (Directory.Exists(downloadFolder))
            {
                var dirs = Utils.GetSubdir(downloadFolder);
                // If the folder already exists, check to see if the config matches. If it does, we don't need to do anything.
                var configProvider = new LocalConfigProvider(dirs, Program.PackageFileName, Program.LocalFileName);
                var localDepConfig = configProvider.GetConfig();
                if (localDepConfig is null || localDepConfig.Info is null || dependencyConfig.Config.Info.Version != localDepConfig.Info.Version)
                {
                    // We delete the directory since it requires the repo to be cloned
                    // While cloning, we MUST have an empty directory
                    // While it is unfortunate we download the .so before we clone, causing this to redownload the so
                    // it's not a big deal and this explanation is just for anyone else that stumbles upon this
                    Utils.DeleteDirectory(downloadFolder);
                    return false;
                }
                return true;
            }
            return false;
        }

        private void HandleGithubLink(string url, in SharedConfig sharedConfig, in Dictionary<string, JsonElement> data, string downloadFolder)
        {
            // If we have a github link, we need to create an archive download link
            // We actually want to clone so we can get our submodules
            // We want to "git clone thing.git"
            // Followed by "git checkout branchName"
            // And "git submodule update --init --recursive"
            // branch is first determined from dependency AdditionalData
            // TODO: Also add support/handling for tags, commits <-- So much for this, Fern
            string branchName = DefaultBranch;
            if (!data.TryGetValue(SupportedPropertiesCommand.BranchName, out var branchNameE))
            {
                // Otherwise, check config
                if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.BranchName, out branchNameE))
                    // Otherwise, use DefaultBranchName
                    branchName = branchNameE.GetString();
            }
            else
                branchName = branchNameE.GetString();

            bool hasSubFolder = data.TryGetValue(SupportedPropertiesCommand.Subfolder, out var subFolder);

            // git clone (url + .git), checout correct branch, and initialize submodules
            if (!DependencyCached(downloadFolder, sharedConfig))
            {
                string origDownloadFolder = downloadFolder;
                if (hasSubFolder)
                {
                    // We have a specified subfolder in our repo. Lets make sure we clone our repo and ONLY use that.
                    downloadFolder = Path.Combine(downloadFolder, "tmp");
                    if (Directory.Exists(downloadFolder))
                    {
                        Utils.DeleteDirectory(downloadFolder);
                    }
                    Utils.CreateDirectory(downloadFolder);
                }
                Console.WriteLine($"Trying to clone from: {url}.git to: {downloadFolder}");
                // This may not always be the case
                try
                {
                    var proc = new ProcessStartInfo("git", $"clone -b {branchName} {url}.git {downloadFolder} --recurse-submodules --no-tags --depth 1")
                    {
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    var p = Process.Start(proc)!;
                    p.OutputDataReceived += P_OutputDataReceived;
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        Console.WriteLine("Attempt to clone using git failed!");
                        Repository.Clone(url + ".git", downloadFolder, new CloneOptions { BranchName = branchName, RecurseSubmodules = true });
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Attempt to clone using git failed!");
                    Repository.Clone(url + ".git", downloadFolder, new CloneOptions { BranchName = branchName, RecurseSubmodules = true });
                }
                if (!Directory.Exists(downloadFolder))
                {
                    // If we STILL don't have a download folder properly populated here, we error very loudly.
                    throw new InvalidOperationException($"Could not clone! Folder: {downloadFolder} not populated!");
                }

                if (hasSubFolder)
                {
                    // If we have a subfolder and we have cloned, we need to grab our subfolder ONLY and bring that to our top level, then delete tmp.
                    Utils.CopyDirectory(Path.Combine(downloadFolder, subFolder.GetString()!), origDownloadFolder);
                    Utils.DeleteDirectory(downloadFolder);
                }
                Utils.DirectoryPermissions(downloadFolder);
            }
        }

        private void P_OutputDataReceived(object sender, DataReceivedEventArgs e) => Console.WriteLine(e.Data);

        public async Task<SharedConfig?> GetSharedConfig(RestoredDependencyPair pair)
        {
            var dependency = pair.Dependency!;
            if (cached.TryGetValue(pair, out var conf))
                return conf;
            if (!dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // Try to download dependency
                try
                {
                    conf = await api.GetLatestConfig(dependency, pair.Version).ConfigureAwait(false);
                }
                catch (WebException)
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    var path = localE.GetString()!;
                    var cfgProv = new LocalConfigProvider(path, Program.PackageFileName, Program.LocalFileName);
                    conf = cfgProv.GetSharedConfig();
                }
                catch
                {
                    return null;
                }
            }
            if (conf is not null)
                cached.Add(pair, conf);
            return conf;
        }

        private void CopyAdditionalData(JsonElement elem, string root, string dst)
        {
            Console.WriteLine($"Copying additional data from: {root} to: {dst}");
            // There's no longer any need to resolve our includes. They should literally be fine as-is.
            // Copy all extra data
            // TODO: Hash check additional data
            foreach (var item in elem.EnumerateArray())
            {
                var location = Path.Combine(root, item.GetString()!);
                if (File.Exists(location))
                {
                    var dest = Path.GetFullPath(Path.Combine(dst, item.GetString()!));

                    Utils.CreateDirectory(Path.GetDirectoryName(dest)!);

                    Utils.SymlinkOrCopyFile(location, dest);
                }
                else if (Directory.Exists(location))
                {
                    var dest = Path.GetFullPath(Path.Combine(dst, item.GetString()!));
                    // Get the parent directory
                    var destFullParent =
                        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dest))!;

                    if (!Directory.Exists(destFullParent))
                    {
                        // Create parent directories
                        Utils.CreateDirectory(destFullParent);
                    }
                    // Delete existing dir
                    else if (Directory.Exists(dest))
                    {
                        Utils.DeleteDirectory(dest);
                    }

                    if (Program.Config.UseSymlinks)
                        Console.WriteLine($"Creating symlink for additional data {location} to {dest}");
                    Utils.SymLinkOrCopyDirectory(location, dest);
                }
            }
        }

        private void CopyTo(string downloadFolder, in Config myConfig, in SharedConfig sharedConfig, in Dictionary<string, JsonElement> data)
        {
            var baseDst = Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id);
            var dst = Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id, sharedConfig.Config.SharedDir);
            var dstExpanded = Path.GetFullPath(dst);
            var root = Utils.GetSubdir(downloadFolder);
            var src = Path.Combine(root, sharedConfig.Config.SharedDir);
            // If we can't get the hash, we need to copy.
            if (!Directory.Exists(dst) || (!Utils.FolderHash(src)?.SequenceEqual(Utils.FolderHash(dst) ?? Array.Empty<byte>()) ?? true))
            {
                Console.WriteLine($"Copying: {src} to: {dst}");

                if (!Directory.Exists(baseDst))
                    // Create parent directories
                    Utils.CreateDirectory(baseDst);

                Utils.SymLinkOrCopyDirectory(src, dstExpanded);
            }

            // Combine the two, if there are two
            // TODO: Add hashing for additional data
            if (data.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemDep))
            {
                CopyAdditionalData(elemDep, root, Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id));
            }
            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemConfig))
            {
                CopyAdditionalData(elemConfig, root, Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id));
            }
        }

        private async Task DownloadDependency(string downloadFolder, Uri url)
        {
            // We would like to throw here on failure
            var downloadLoc = downloadFolder + ".zip";
            Console.WriteLine($"Trying to download from: {url}");
            var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
            if (File.Exists(downloadLoc))
                File.Delete(downloadLoc);
            using (var fs = File.OpenWrite(downloadLoc))
                await stream.CopyToAsync(fs).ConfigureAwait(false);
            // We would like to throw here on failure
            if (Directory.Exists(downloadFolder))
                Utils.DeleteDirectory(downloadFolder);
            ZipFile.ExtractToDirectory(downloadLoc, downloadFolder, true);
            Utils.DirectoryPermissions(downloadFolder);

            // Use url provided in config to grab folders specified by config and place them under our own
            // If the shared folder doesn't exist, throw

            File.Delete(downloadLoc);
        }

        // TODO: Add SharedConfig parameter
        public async Task ResolveDependency(Config myConfig, RestoredDependencyPair pair)
        {
            if (pair.Dependency is null)
                throw new ArgumentException("Dependency pair cannot have a null Dependency!", nameof(pair));
            var sharedConfig = await GetSharedConfig(pair).ConfigureAwait(false);
            if (sharedConfig is null)
                throw new DependencyException($"Could not get shared config for dependency pair: {pair.Dependency.Id} version range: {pair.Dependency.VersionRange} specific version: {pair.Version}");
            if (sharedConfig.Config is null || sharedConfig.Config.Info is null)
                throw new DependencyException($"Shared config is invalid for dependency pair: {pair.Dependency.Id} version range: {pair.Dependency.VersionRange} specific version: {pair.Version}");
            var dependency = pair.Dependency;

            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.HeadersOnly, out var headerE) && headerE.GetBoolean())
            {
                // If we are restoring headersOnly, it will be handled in the unique resolve dependency.
                // Instead, exit.
                return;
            }
            // If we are LOCAL, however, we simply need to check to make sure if we want release or debug
            // Then we look in the local repo for those files
            // If we find them, copy them over
            // If we cannot find them, use the links.
            // If we STILL cannot find them, we exit
            // Handle obtaining .so file from external config
            // Grab the .so file link from AdditionalData and handle it
            // First, try to see if we have a debugSoLink. If we do, AND we either: don't have releaseSo OR it is set to false, use it
            // Otherwise, use the release so link.
            string soLink = "";
            bool useRelease = false;
            if (dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.UseReleaseSo, out var releaseE) && releaseE.GetBoolean())
                useRelease = true;

            // If it does, use it.
            // Otherwise, throw an error
            string style = "";
            if (dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.StyleToUse, out var styleStr))
            {
                var tmp = styleStr.GetString();
                if (tmp is null)
                    throw new DependencyException($"StyleToUse: {tmp} cannot be null!");
                style = tmp;
            }

            if (style != null)
            {
                // If we provide a style that we would like to use, we need to see if that style exists.
                // Check the config
                if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.Styles, out var styles))
                {
                    bool found = false;
                    foreach (var s in styles.EnumerateArray())
                    {
                        var st = SupportedPropertiesCommand.Convert<SupportedPropertiesCommand.StyleProperty>(s)!;
                        // If s is a valid style (it should be)
                        if (st.Name == style)
                        {
                            // Use this style
                            soLink = string.IsNullOrEmpty(st.DebugSoLink) && !useRelease
                                ? throw new DependencyException($"Style: {style} with must have a non-null: {nameof(st.DebugSoLink)}!")
                                : st.DebugSoLink!;
                            soLink = string.IsNullOrEmpty(soLink) && string.IsNullOrEmpty(st.SoLink)
                                ? throw new DependencyException($"Dependency: {sharedConfig.Config.Info.Id}, using style: {style} has no {nameof(st.SoLink)} property! Cannot download so to link!")
                                : st.SoLink!;
                            found = true;
                            break;
                        }
                        else
                            throw new DependencyException($"Style in resolved dependency: {sharedConfig.Config.Info.Id} does not have a valid {nameof(st.Name)} property!");
                    }
                    if (!found)
                        // Throw if we can't find the dependency
                        throw new DependencyException($"Resolved dependency: {sharedConfig.Config.Info.Id} does not have style: {style}!");
                }
            }

            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.DebugSoLink, out var soLinkE))
            {
                var tmp = soLinkE.GetString();
                if (tmp is null)
                    throw new DependencyException($"DebugSoLink: {tmp} cannot be null!");
                soLink = tmp;
            }
            // If we have no debug link, we must get it from the release so link.
            // If we cannot get it, we have to throw
            soLink = string.IsNullOrEmpty(soLink) && !sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.ReleaseSoLink, out soLinkE)
                ? throw new DependencyException($"Dependency: {sharedConfig.Config.Info.Id} has no 'soLink' property! Cannot download so to link!")
                : soLinkE.GetString()!;

            // soName is dictated by the overriden name, if it exists. Otherwise, it is this.
            var soName = sharedConfig.Config.Info.GetSoName(out var overrodeName);

            if (!(soName is null))
            {
                var tempLoc = Utils.GetLibrary(sharedConfig.Config.Info, soName);
                var fileLoc = Path.Combine(myConfig.DependenciesDir, soName);
                var fullFileLoc = Path.GetFullPath(fileLoc);

                if (!File.Exists(fileLoc) || overrodeName)
                {
                    // If we have a file here already, we simply perform the modifications and call it a day
                    // AND we are not a specifically named file (since if we are, we need to overwrite cache)
                    if (File.Exists(tempLoc) && !overrodeName)
                    {
                        // Make a symlink from the cache, or fallback to copy
                        // Copy the temp file to our current, then make sure we setup everything
                        Utils.SymlinkOrCopyFile(tempLoc, fullFileLoc);
                    }
                    else
                    {
                        var tempDirLoc = Path.GetDirectoryName(tempLoc);
                        if (tempDirLoc != null && !Directory.Exists(tempDirLoc))
                            Utils.CreateDirectory(tempDirLoc);

                        if (!File.Exists(tempLoc))
                        {
                            Console.WriteLine($"Downloading so from: {soLink} to: {tempLoc}");
                            var stream = await client.GetStreamAsync(soLink).ConfigureAwait(false);
                            if (File.Exists(tempLoc))
                                File.Delete(tempLoc);
                            using var fs = File.OpenWrite(tempLoc);
                            await stream.CopyToAsync(fs).ConfigureAwait(false);
                        }

                        // Make a symlink from the cache, or fallback to copy
                        // Copy the temp file to our current, then make sure we setup everything
                        Utils.SymlinkOrCopyFile(tempLoc, fullFileLoc);
                    }
                }
                else
                {
                    Console.WriteLine($"Found existing: {fileLoc} for: {sharedConfig.Config.Info.Id} - version: {sharedConfig.Config.Info.Version}");
                }
                // Perform modifications to the Android.mk and c_cpp_properties.json as necessary (I don't think c_cpp_properties.json should change, includePath is constant)
                // But Android.mk needs some things changed:
                // It needs a new module
                // It needs to set some stuff on that new module
                // It needs to use that new module in main build

                var mk = GetMk();
                if (mk != null)
                {
                    string buildLine = sharedConfig.Config.Info.IsStaticLinking()
                        ? "include $(PREBUILT_STATIC_LIBRARY)"
                        : "include $(PREBUILT_SHARED_LIBRARY)";

                    var module = new Data.Module
                    {
                        PrefixLines = new List<string>
                        {
                            $"# Creating prebuilt for dependency: {sharedConfig.Config.Info.Id} - version: {sharedConfig.Config.Info.Version}",
                            "include $(CLEAR_VARS)"
                        },
                        Src = new List<string>
                        {
                            fileLoc.Replace('\\', '/')
                        },
                        ExportIncludes = new List<string>
                        {
                            Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id).Replace('\\', '/')
                        },
                        BuildLine = buildLine
                    };
                    if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.CompileOptions, out var optE))
                    {
                        var res = SupportedPropertiesCommand.Convert<SupportedPropertiesCommand.CompileOptionsProperty>(optE);
                        if (res is null)
                            throw new DependencyException($"Dependency: {dependency.Id} with version: {sharedConfig.Config.Info.Version} does not have a valid: {SupportedPropertiesCommand.CompileOptions} property!");
                        module.ExportIncludes.AddRange(res.IncludePaths);
                        module.ExportCFlags.AddRange(res.CFlags);
                        foreach (var l in res.SystemIncludes)
                            module.AddSystemInclude(l, true);
                        module.ExportCppFlags.AddRange(res.CppFlags);
                        foreach (var l in res.CppFeatures)
                            module.AddExportCppFeature(l);
                    }
                    if (overrodeName)
                        module.Id = soName.ReplaceFirst("lib", "").ReplaceLast(".so", "").ReplaceLast(".a", "");
                    else
                        module.EnsureIdIs(sharedConfig.Config.Info.Id, sharedConfig.Config.Info.Version);
                    var main = mk.Modules.LastOrDefault();
                    if (main != null)
                    {
                        // Should this be its own method?
                        void HandleLibs(List<string> libsList)
                        {
                            // TODO: Probably a stupid check, but should be backed up (?) so should be more or less ok?
                            // For matching modules with names: beatsaber-hook_0_3_0 for replacing with beatsaber-hook_0_4_4
                            int lib = libsList.FindIndex(s =>
                                overrodeName
                                    ? s.TrimStart().Equals(module.Id, StringComparison.OrdinalIgnoreCase)
                                    : s.TrimStart().StartsWith(sharedConfig.Config.Info.Id,
                                        StringComparison.OrdinalIgnoreCase));

                            if (lib < 0)
                            {
                                // module.Id is forcibly set just above
                                libsList.Add(module.Id!);
                                var matchingModuleIndex = mk.Modules.FindIndex(m =>
                                    (overrodeName
                                        ? m.Id?.TrimStart().Equals(module.Id, StringComparison.OrdinalIgnoreCase)
                                        : m.Id?.TrimStart().StartsWith(sharedConfig.Config.Info.Id,
                                            StringComparison.OrdinalIgnoreCase)) ?? false);
                                if (matchingModuleIndex == -1)
                                {
                                    // Add if it didn't already exist
                                    mk.Modules.Insert(mk.Modules.Count - 1, module);
                                }
                                else
                                {
                                    // Overwrite if it does
                                    var matchingModule = mk.Modules[matchingModuleIndex];
                                    matchingModule.PrefixLines = module.PrefixLines;
                                    matchingModule.Id = module.Id;
                                    matchingModule.Src = module.Src;
                                    matchingModule.ExportIncludes = module.ExportIncludes;
                                }
                            }
                            else
                            {
                                // If we find a matching module, we need to see if our version is higher than it.
                                // If it is, we overwrite. Otherwise, do nothing.
                                // Also, if the src matches exactly, we don't have to worry.
                                var exists = libsList[lib];
                                var version = new SemVer.Version(0, 0, 0);
                                if (!overrodeName)
                                {
                                    exists = exists.TrimStart();
                                    if (exists.Length > sharedConfig.Config.Info.Id.Length + 1)
                                        exists = exists[(sharedConfig.Config.Info.Id.Length + 1)..];
                                }
                                else
                                {
                                    exists = exists.TrimStart()[module.Id!.Length..];
                                }

                                try
                                {
                                    version = new SemVer.Version(exists.Replace('_', '.'));
                                }
                                catch (ArgumentException)
                                {
                                    // If we cannot parse the version, always overwrite.
                                }

                                if (version < sharedConfig.Config.Info.Version)
                                {
                                    // If the version we want to add is greater than the version already in there, we replace it.
                                    var matchingModuleIndex = mk.Modules.FindIndex(m =>
                                        (overrodeName
                                            ? m.Id?.TrimStart().Equals(module.Id, StringComparison.OrdinalIgnoreCase)
                                            : m.Id?.TrimStart().StartsWith(sharedConfig.Config.Info.Id,
                                                StringComparison.OrdinalIgnoreCase)) ?? false);
                                    if (matchingModuleIndex == -1)
                                    {
                                        // Add if it didn't already exist
                                        mk.Modules.Insert(mk.Modules.Count - 1, module);
                                    }
                                    else
                                    {
                                        // Overwrite if it does
                                        var matchingModule = mk.Modules[matchingModuleIndex];
                                        matchingModule.PrefixLines = module.PrefixLines;
                                        matchingModule.Id = module.Id;
                                        matchingModule.Src = module.Src;
                                        matchingModule.ExportIncludes = module.ExportIncludes;
                                    }

                                    libsList[lib] = module.Id!;
                                }
                            }
                        }

                        // Only add to list if applicable.
                        // If static list but not static lib
                        if (sharedConfig.Config.Info.IsStaticLinking())
                        {
                            HandleLibs(main.StaticLibs);
                        }
                        else
                        {
                            HandleLibs(main.SharedLibs);
                        }
                    }
                }
            }
        }

        public async Task ResolveUniqueDependency(Config myConfig, KeyValuePair<RestoredDependencyPair, SharedConfig> resolved)
        {
            // When we resolve a unique dependency, we copy over the headers.
            // Otherwise, we simply copy over the .so and call it a day (unless it is header-only)
            var data = resolved.Key.Dependency.AdditionalData;
            var conf = resolved.Value;
            if (!data.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // If not local, perform remote obtain
                var url = conf.Config.Info.Url;

                var downloadFolder = Utils.GetSource(conf.Config.Info);

                if (!Directory.Exists(downloadFolder))
                    Utils.CreateDirectory(downloadFolder);

                if (IsGithubLink(url))
                {
                    // Attempt to handle the github link by cloning {url}.git and all submodules
                    HandleGithubLink(url.ToString().TrimEnd('/'), conf, data, downloadFolder);
                }
                else
                {
                    // Attempt to download the file as a zip and extract it
                    if (!DependencyCached(downloadFolder, conf))
                        await DownloadDependency(downloadFolder, url).ConfigureAwait(false);
                }
                var root = Utils.GetSubdir(downloadFolder);
                var externalCfgProvider = new LocalConfigProvider(root, Program.PackageFileName, Program.LocalFileName);
                var externalCfg = externalCfgProvider.GetConfig();
                if (externalCfg is null || externalCfg.Info is null || conf.Config.Info.Version != externalCfg.Info.Version || !conf.Config.Info.Version.Equals(externalCfg.Info.Version))
                {
                    throw new DependencyException($"Could not resolve dependency: {conf.Config.Info.Id}! Downloaded config {(externalCfg is not null && externalCfg.Info is not null ? externalCfg.Info.Version : "NULL")} does not match obtained config {conf.Config.Info.Version}!");
                }
                CopyTo(downloadFolder, myConfig, conf, data);
            }
            else
            {
                var localPath = localE.GetString()!;
                // Copy the localPath folder to myConfig
                CopyTo(localPath, myConfig, conf, data);
            }
        }

        public void RemoveDependency(in Config myConfig, in Dependency dependency) => Directory.Delete(Path.Combine(myConfig.DependenciesDir, dependency.Id), true);
    }
}