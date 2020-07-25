using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    internal class RemoteQPMDependencyResolver : IDependencyResolver
    {
        private readonly WebClient client;
        private readonly QPMApi api;
        private readonly Dictionary<Dependency, Config> cached = new Dictionary<Dependency, Config>();

        public event Action<Config, Config> OnDependencyResolved;

        public RemoteQPMDependencyResolver(QPMApi api)
        {
            client = new WebClient();
            this.api = api;
        }

        private const string DownloadGithubUrl = "https://github.com";
        private const string DefaultBranch = "master";

        private bool IsGithubLink(Uri uri) => uri.AbsoluteUri.StartsWith(DownloadGithubUrl);

        public Config GetConfig(Dependency dependency)
        {
            if (cached.TryGetValue(dependency, out var conf))
                return conf;
            // Try to download dependency
            try
            {
                conf = api.GetLatestConfig(dependency);
            }
            catch (WebException)
            {
                return null;
            }
            // Download text from url
            // Read config from text
            cached.Add(dependency, conf);
            return conf;
        }

        public void ResolveDependency(in Config myConfig, in Dependency dependency)
        {
            if (!cached.TryGetValue(dependency, out var config))
                config = GetConfig(dependency);

            var url = config.Info.Url;

            if (IsGithubLink(url))
            {
                // If we have a github link, we need to create an archive download link
                // branch is first determined from dependency AdditionalData
                // TODO: Also add support/handling for tags, commits
                if (!dependency.AdditionalData.TryGetValue("branchName", out var branchName))
                    // Otherwise, check config
                    if (!config.AdditionalData.TryGetValue("branchName", out branchName))
                        // Otherwise, use DefaultBranchName
                        branchName = DefaultBranch;
                var segs = url.Segments.ToList();
                segs.Add("/");
                segs.Add("archive/");
                segs.Add(branchName + ".zip");
                url = new Uri(DownloadGithubUrl + string.Join("", segs));
            }
            // Attempt to download the file as a zip
            var outter = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QPM");
            if (!Directory.Exists(outter))
                Directory.CreateDirectory(outter);
            var downloadFolder = Path.Combine(outter, dependency.Id);
            var downloadLoc = downloadFolder + ".zip";
            // We would like to throw here on failure
            if (File.Exists(downloadLoc))
                File.Delete(downloadLoc);
            Console.WriteLine($"Trying to download from: {url}");
            client.DownloadFile(url, downloadLoc);
            // We would like to throw here on failure
            if (Directory.Exists(downloadFolder))
                Utils.DeleteDirectory(downloadFolder);
            ZipFile.ExtractToDirectory(downloadLoc, downloadFolder, true);

            // Use url provided in config to grab folders specified by config and place them under our own
            // If the shared folder doesn't exist, throw
            var actualRoot = downloadFolder;
            var dirs = Directory.GetDirectories(actualRoot);
            while (dirs.Length == 1 && Directory.GetFiles(actualRoot).Length == 0)
            {
                // If we have only one folder and no files, chances are we have to go one level deeper
                actualRoot = dirs[0];
                dirs = Directory.GetDirectories(actualRoot);
            }
            var dst = Path.Combine(myConfig.DependenciesDir, config.Info.Id);
            if (Directory.Exists(dst))
                Utils.DeleteDirectory(dst);
            Directory.Move(Path.Combine(actualRoot, config.SharedDir), dst);
            File.Delete(downloadLoc);

            OnDependencyResolved?.Invoke(myConfig, config);
        }

        public void RemoveDependency(in Config myConfig, in Dependency dependency) => Directory.Delete(Path.Combine(myConfig.DependenciesDir, dependency.Id), true);
    }
}