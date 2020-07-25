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

        private bool IsGithubLink(Uri uri) => uri.Fragment.StartsWith("github");

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
                    if (!config.AdditionalData.TryGetValue("localBranchName", out branchName))
                        // Otherwise, use DefaultBranchName
                        branchName = DefaultBranch;
                var segs = url.Segments.ToList();
                segs.Add("archive/");
                segs.Add(branchName + ".zip");
                url = new Uri(DownloadGithubUrl + string.Join("", segs));
            }
            // Attempt to download the file as a zip
            var downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), dependency.Id);
            var downloadLoc = downloadFolder + ".zip";
            // We would like to throw here on failure
            client.DownloadFile(url, downloadLoc);
            // We would like to throw here on failure
            ZipFile.ExtractToDirectory(downloadLoc, downloadFolder);

            // Use url provided in config to grab folders specified by config and place them under our own
            // If the shared folder doesn't exist, throw
            Directory.Move(Path.Combine(downloadFolder, config.SharedDir), Path.Combine(myConfig.DependenciesDir, config.Info.Id));

            OnDependencyResolved?.Invoke(myConfig, config);
        }
    }
}