using LibGit2Sharp;
using QPM.Commands;
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
        private readonly Dictionary<Dependency, Config> cached = new Dictionary<Dependency, Config>();

        public event Action<Config, Config, Dependency> OnDependencyResolved;

        public RemoteQPMDependencyResolver(QPMApi api)
        {
            client = new WebClient();
            this.api = api;
        }

        private const string DownloadGithubUrl = "https://github.com";
        private const string DefaultBranch = "master";

        private static readonly HashSet<string> ExtensionsToFix = new HashSet<string>
        {
            ".cpp",
            ".c",
            ".hpp",
            ".h"
        };

        private bool IsGithubLink(Uri uri) => uri.AbsoluteUri.StartsWith(DownloadGithubUrl);

        private bool DependencyCached(string downloadFolder, in Config dependencyConfig)
        {
            if (Directory.Exists(downloadFolder))
            {
                var dirs = Utils.GetSubdir(downloadFolder);
                // If the folder already exists, check to see if the config matches. If it does, we don't need to do anything.
                var configProvider = new LocalConfigProvider(dirs, Program.PackageFileName, Program.LocalFileName);
                var localDepConfig = configProvider.GetConfig();
                if (localDepConfig is null || localDepConfig.Info is null || dependencyConfig.Info.Version != localDepConfig.Info.Version)
                {
                    Utils.DeleteDirectory(downloadFolder);
                    return false;
                }
                return true;
            }
            return false;
        }

        private void HandleGithubLink(Uri url, in Config config, in Dependency dependency, string downloadFolder)
        {
            // If we have a github link, we need to create an archive download link
            // We actually want to clone so we can get our submodules
            // We want to "git clone thing.git"
            // Followed by "git checkout branchName"
            // And "git submodule update --init --recursive"
            // branch is first determined from dependency AdditionalData
            // TODO: Also add support/handling for tags, commits
            string branchName = DefaultBranch;
            if (!dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.BranchName, out var branchNameE))
            {
                // Otherwise, check config
                if (config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.BranchName, out branchNameE))
                    // Otherwise, use DefaultBranchName
                    branchName = branchNameE.GetString();
            }
            else
                branchName = branchNameE.GetString();

            // git clone (url + .git), checout correct branch, and initialize submodules
            if (!DependencyCached(downloadFolder, config))
            {
                Console.WriteLine($"Trying to clone from: {url}.git");
                Repository.Clone(url + ".git", downloadFolder, new CloneOptions { BranchName = branchName, RecurseSubmodules = true });
            }
        }

        public Config GetConfig(Dependency dependency)
        {
            if (cached.TryGetValue(dependency, out var conf))
                return conf;
            if (!dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // Try to download dependency
                try
                {
                    conf = api.GetLatestConfig(dependency);
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
                    conf = cfgProv.GetConfig();
                }
                catch
                {
                    return null;
                }
            }
            cached.Add(dependency, conf);
            return conf;
        }

        private void CopyAdditionalData(JsonElement elem, string root, string dst, string sharedDir)
        {
            var sharedStr = sharedDir + "/";
            Console.WriteLine("Copying additional data...");
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

        private void CopyTo(string downloadFolder, in Config myConfig, in Config config, in Dependency dependency)
        {
            var dst = Path.Combine(myConfig.DependenciesDir, config.Info.Id, config.SharedDir);
            if (Directory.Exists(dst))
                Utils.DeleteDirectory(dst);
            var root = Utils.GetSubdir(downloadFolder);
            Utils.CopyDirectory(Path.Combine(root, config.SharedDir), dst);
            // Combine the two, if there are two
            if (dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemDep))
            {
                CopyAdditionalData(elemDep, root, dst, config.SharedDir);
            }
            if (config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.AdditionalFiles, out var elemConfig))
            {
                CopyAdditionalData(elemConfig, root, dst, config.SharedDir);
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

        public void ResolveDependency(in Config myConfig, in Dependency dependency)
        {
            if (!cached.TryGetValue(dependency, out var config))
                config = GetConfig(dependency);

            if (!dependency.AdditionalData.TryGetValue(SupportedPropertiesCommand.LocalPath, out var localE))
            {
                // If not local, perform remote obtain

                var url = config.Info.Url;
                var outter = Utils.GetTempDir();
                var downloadFolder = Path.Combine(outter, dependency.Id);

                if (IsGithubLink(url))
                {
                    // Attempt to handle the github link by cloning {url}.git and all submodules
                    HandleGithubLink(url, config, dependency, downloadFolder);
                }
                else
                {
                    // Attempt to download the file as a zip and extract it
                    if (!DependencyCached(downloadFolder, config))
                        DownloadDependency(downloadFolder, url);
                }
                var root = Utils.GetSubdir(downloadFolder);
                var externalCfgProvider = new LocalConfigProvider(root, Program.PackageFileName, Program.LocalFileName);
                var externalCfg = externalCfgProvider.GetConfig();
                if (externalCfg is null || externalCfg.Info is null || config.Info.Version != externalCfg.Info.Version || !dependency.VersionRange.IsSatisfied(externalCfg.Info.Version))
                {
                    throw new DependencyException($"Could not resolve dependency: {dependency.Id}! Downloaded config does not match obtained config!");
                }
                CopyTo(downloadFolder, myConfig, config, dependency);
            }
            else
            {
                var localPath = localE.GetString();
                // Copy the localPath folder to myConfig
                CopyTo(localPath, myConfig, config, dependency);
            }
            OnDependencyResolved?.Invoke(myConfig, config, dependency);
        }

        public void RemoveDependency(in Config myConfig, in Dependency dependency) => Directory.Delete(Path.Combine(myConfig.DependenciesDir, dependency.Id), true);
    }
}