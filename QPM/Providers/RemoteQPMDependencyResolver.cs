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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QPM
{
    internal class RemoteQPMDependencyResolver : IDependencyResolver
    {
        private readonly WebClient client;
        private readonly QPMApi api;
        private readonly Dictionary<Dependency, SharedConfig> cached = new Dictionary<Dependency, SharedConfig>();
        private readonly AndroidMkProvider androidMkProvider;

        public RemoteQPMDependencyResolver(QPMApi api, AndroidMkProvider mkProvider)
        {
            client = new WebClient();
            this.api = api;
            androidMkProvider = mkProvider;
        }

        private const string DownloadGithubUrl = "https://github.com";
        private const string DefaultBranch = "master";

        private bool _gotMk = false;
        private AndroidMk _cachedMk;

        private AndroidMk GetMk()
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
            // TODO: Also add support/handling for tags, commits
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

            // git clone (url + .git), checout correct branch, and initialize submodules
            if (!DependencyCached(downloadFolder, sharedConfig))
            {
                Console.WriteLine($"Trying to clone from: {url}.git to: {downloadFolder}");
                // This may not always be the case
                bool recurse = data.ContainsKey(SupportedPropertiesCommand.AdditionalFiles);
                Repository.Clone(url + ".git", downloadFolder, new CloneOptions { BranchName = branchName, RecurseSubmodules = recurse });
                Utils.DirectoryPermissions(downloadFolder);
            }
        }

        public SharedConfig GetSharedConfig(RestoredDependencyPair pair)
        {
            var dependency = pair.Dependency!;
            if (cached.TryGetValue(dependency, out var conf))
                return conf;
            if (!dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // Try to download dependency
                try
                {
                    conf = api.GetLatestConfig(dependency, pair.Version);
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
                    var path = localE.GetString();
                    var cfgProv = new LocalConfigProvider(path, Program.PackageFileName, Program.LocalFileName);
                    conf = cfgProv.GetSharedConfig();
                }
                catch
                {
                    return null;
                }
            }
            cached.Add(dependency, conf);
            return conf;
        }

        private void CopyAdditionalData(JsonElement elem, string root, string dst)
        {
            Console.WriteLine($"Copying additional data from: {root} to: {dst}");
            // There's no longer any need to resolve our includes. They should literally be fine as-is.
            // Copy all extra data
            foreach (var item in elem.EnumerateArray())
            {
                var location = Path.Combine(root, item.GetString());
                if (File.Exists(location))
                {
                    var dest = Path.Combine(dst, item.GetString());
                    File.Copy(location, dest);
                }
                else if (Directory.Exists(location))
                {
                    Utils.CopyDirectory(location, Path.Combine(dst, item.GetString()));
                }
            }
        }

        private void CopyTo(string downloadFolder, in Config myConfig, in SharedConfig sharedConfig, in Dictionary<string, JsonElement> data)
        {
            var dst = Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id, sharedConfig.Config.SharedDir);
            if (Directory.Exists(dst))
                Utils.DeleteDirectory(dst);
            var root = Utils.GetSubdir(downloadFolder);
            var src = Path.Combine(root, sharedConfig.Config.SharedDir);
            Console.WriteLine($"Copying: {src} to: {dst}");
            Utils.CopyDirectory(src, dst);
            // Combine the two, if there are two
            if (data.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemDep))
            {
                CopyAdditionalData(elemDep, root, dst);
            }
            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemConfig))
            {
                CopyAdditionalData(elemConfig, root, dst);
            }
        }

        private void DownloadDependency(string downloadFolder, Uri url)
        {
            // We would like to throw here on failure
            var downloadLoc = downloadFolder + ".zip";
            Console.WriteLine($"Trying to download from: {url}");
            client.DownloadFile(url, downloadLoc);
            // We would like to throw here on failure
            if (Directory.Exists(downloadFolder))
                Utils.DeleteDirectory(downloadFolder);
            ZipFile.ExtractToDirectory(downloadLoc, downloadFolder, true);

            // Use url provided in config to grab folders specified by config and place them under our own
            // If the shared folder doesn't exist, throw

            File.Delete(downloadLoc);
        }

        // TODO: Add SharedConfig parameter
        public void ResolveDependency(in Config myConfig, in RestoredDependencyPair pair)
        {
            var sharedConfig = GetSharedConfig(pair);
            if (sharedConfig is null)
                throw new DependencyException($"Could not get shared config for dependency pair: {pair.Dependency.Id} version range: {pair.Dependency.VersionRange} specific version: {pair.Version}");
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
            string soLink = null;
            bool useRelease = false;
            if (dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.UseReleaseSo, out var releaseE) && releaseE.GetBoolean())
                useRelease = true;

            // If it does, use it.
            // Otherwise, throw an error
            string style = null;
            if (dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.StyleToUse, out var styleStr))
                style = styleStr.GetString();

            if (style != null)
            {
                // If we provide a style that we would like to use, we need to see if that style exists.
                // Check the config
                if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.Styles, out var styles))
                {
                    bool found = false;
                    foreach (var s in styles.EnumerateArray())
                    {
                        // If s is a valid style (it should be)
                        if (s.TryGetProperty(SupportedPropertiesCommand.Style_Name, out var styleName) && styleName.GetString() == style)
                        {
                            // Use this style
                            if (s.TryGetProperty(SupportedPropertiesCommand.DebugSoLink, out var soLinkEStyle) && !useRelease)
                                soLink = soLinkEStyle.GetString();
                            soLink = soLink is null && !s.TryGetProperty(SupportedPropertiesCommand.ReleaseSoLink, out soLinkEStyle)
                                ? throw new DependencyException($"Dependency: {sharedConfig.Config.Info.Id}, using style: {style} has no 'soLink' property! Cannot download so to link!")
                                : soLinkEStyle.GetString();
                            found = true;
                            break;
                        }
                        else
                            throw new DependencyException($"Style in resolved dependency: {sharedConfig.Config.Info.Id} does not have a {SupportedPropertiesCommand.Style_Name} property!");
                    }
                    if (!found)
                        // Throw if we can't find the dependency
                        throw new DependencyException($"Resolved dependency: {sharedConfig.Config.Info.Id} does not have style: {style}!");
                }
            }

            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.DebugSoLink, out var soLinkE))
                soLink = soLinkE.GetString();
            // If we have no debug link, we must get it from the release so link.
            // If we cannot get it, we have to throw
            soLink = soLink is null && !sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.ReleaseSoLink, out soLinkE)
                ? throw new DependencyException($"Dependency: {sharedConfig.Config.Info.Id} has no 'soLink' property! Cannot download so to link!")
                : soLinkE.GetString();

            WebClient client = new WebClient();
            // soName is dictated by the overriden name, if it exists. Otherwise, it is this.
            var soName = "lib" + (sharedConfig.Config.Info.Id + "_" + sharedConfig.Config.Info.Version.ToString()).Replace('.', '_') + ".so";
            bool overrodeName = false;
            if (sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.OverrideSoName, out var overridenName))
            {
                overrodeName = true;
                soName = overridenName.GetString();
            }
            var tempLoc = Path.Combine(Utils.GetTempDir(), soName);
            var fileLoc = Path.Combine(myConfig.DependenciesDir, soName);
            if (!File.Exists(fileLoc))
            {
                // If we have a file here already, we simply perform the modifications and call it a day
                if (File.Exists(tempLoc))
                    // Copy the temp file to our current, then make sure we setup everything
                    File.Copy(tempLoc, fileLoc);
                else
                {
                    // We have to download
                    Console.WriteLine($"Downloading so from: {soLink} to: {tempLoc}");
                    client.DownloadFile(soLink, tempLoc);
                    File.Copy(tempLoc, fileLoc);
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
                var module = new Module
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
                    ExportIncludes = Path.Combine(myConfig.DependenciesDir, sharedConfig.Config.Info.Id).Replace('\\', '/'),
                    BuildLine = "include $(PREBUILT_SHARED_LIBRARY)"
                };
                if (overrodeName)
                    module.Id = soName.ReplaceFirst("lib", "").ReplaceLast(".so", "");
                else
                    module.EnsureIdIs(sharedConfig.Config.Info.Id, sharedConfig.Config.Info.Version);
                var main = mk.Modules.LastOrDefault();
                if (main != null)
                {
                    // TODO: Probably a stupid check, but should be backed up (?) so should be more or less ok?
                    // For matching modules with names: beatsaber-hook_0_3_0 for replacing with beatsaber-hook_0_4_4
                    int sharedLib = main.SharedLibs.FindIndex(s => overrodeName ? module.Id.Equals(s, StringComparison.OrdinalIgnoreCase) : s.TrimStart().StartsWith(sharedConfig.Config.Info.Id, StringComparison.OrdinalIgnoreCase));
                    if (sharedLib < 0)
                    {
                        main.SharedLibs.Add(module.Id);
                        var matchingModule = mk.Modules.FindIndex(m => m.Id == module.Id);
                        if (matchingModule == -1)
                        {
                            // Add if it didn't already exist
                            mk.Modules.Insert(0, module);
                        }
                        else
                        {
                            // Overwrite if it does
                            var exists = mk.Modules[matchingModule];
                            exists.Id = module.Id;
                            exists.Src = module.Src;
                            exists.ExportIncludes = module.ExportIncludes;
                        }
                    }
                    else
                    {
                        // If we find a matching module, we need to see if our version is higher than it.
                        // If it is, we overwrite. Otherwise, do nothing.
                        // Also, if the src matches exactly, we don't have to worry.
                        var exists = main.SharedLibs[sharedLib];
                        var version = new SemVer.Version(0, 0, 0);
                        if (!overrodeName)
                        {
                            exists = exists.TrimStart();
                            if (exists.Length < sharedConfig.Config.Info.Id.Length + 1)
                                exists = exists.TrimStart().Substring(sharedConfig.Config.Info.Id.Length + 1);
                        }
                        else
                        {
                            exists = exists.TrimStart().Substring(module.Id.Length);
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
                            var matchingModule = mk.Modules.FindIndex(m => m.Id == main.SharedLibs[sharedLib]);
                            if (matchingModule > -1)
                            {
                                // Overwrite if there is a matching module with an identical ID to the one we just replaced.
                                mk.Modules[matchingModule].Id = module.Id;
                                mk.Modules[matchingModule].Src = module.Src;
                                mk.Modules[matchingModule].ExportIncludes = module.ExportIncludes;
                            }
                            else
                            {
                                mk.Modules.Insert(0, module);
                            }
                            main.SharedLibs[sharedLib] = module.Id;
                        }
                    }
                }
            }
        }

        public void ResolveUniqueDependency(in Config myConfig, KeyValuePair<RestoredDependencyPair, SharedConfig> resolved)
        {
            // When we resolve a unique dependency, we copy over the headers.
            // Otherwise, we simply copy over the .so and call it a day (unless it is header-only)
            var data = resolved.Key.Dependency.AdditionalData;
            var conf = resolved.Value;
            if (!data.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // If not local, perform remote obtain

                var url = conf.Config.Info.Url;
                var outter = Utils.GetTempDir();
                var downloadFolder = Path.Combine(outter, conf.Config.Info.Id);

                if (IsGithubLink(url))
                {
                    // Attempt to handle the github link by cloning {url}.git and all submodules
                    HandleGithubLink(url.ToString().TrimEnd('/'), conf, data, downloadFolder);
                }
                else
                {
                    // Attempt to download the file as a zip and extract it
                    if (!DependencyCached(downloadFolder, conf))
                        DownloadDependency(downloadFolder, url);
                }
                var root = Utils.GetSubdir(downloadFolder);
                var externalCfgProvider = new LocalConfigProvider(root, Program.PackageFileName, Program.LocalFileName);
                var externalCfg = externalCfgProvider.GetConfig();
                if (externalCfg is null || externalCfg.Info is null || conf.Config.Info.Version != externalCfg.Info.Version || !conf.Config.Info.Version.Equals(externalCfg.Info.Version))
                {
                    throw new DependencyException($"Could not resolve dependency: {conf.Config.Info.Id}! Downloaded config does not match obtained config!");
                }
                CopyTo(downloadFolder, myConfig, conf, data);
            }
            else
            {
                var localPath = localE.GetString();
                // Copy the localPath folder to myConfig
                CopyTo(localPath, myConfig, conf, data);
            }
        }

        public void RemoveDependency(in Config myConfig, in Dependency dependency) => Directory.Delete(Path.Combine(myConfig.DependenciesDir, dependency.Id), true);
    }
}