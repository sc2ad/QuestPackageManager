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
    internal class RemoteUriResolver : IUriHandler
    {
        private readonly LocalConfigProvider configReader;
        private readonly WebClient client;
        private readonly Dictionary<Dependency, Config> cached = new Dictionary<Dependency, Config>();

        public RemoteUriResolver(LocalConfigProvider configReader)
        {
            this.configReader = configReader;
            client = new WebClient();
        }

        private const string RawGithubUrl = "https://raw.githubusercontent.com";
        private const string DownloadGithubUrl = "https://github.com";
        private const string DefaultBranch = "master";

        private bool IsGithubLink(Uri uri) => uri.Fragment.StartsWith("github");

        public Config GetConfig(Dependency dependency)
        {
            if (cached.TryGetValue(dependency, out var conf))
                return conf;
            if (dependency.Url is null)
                return null;
            var url = dependency.Url;
            if (IsGithubLink(dependency.Url))
            {
                // See if we have a branch in additionalData
                if (!dependency.AdditionalData.TryGetValue("branchName", out var branchName))
                    // Otherwise, use DefaultBranchName
                    branchName = DefaultBranch;
                // Create correct segments
                var segs = dependency.Url.Segments.ToList();
                segs.Add(branchName + "/");
                segs.Add(Program.PackageFileName);
                // Create raw link for specific file
                url = new Uri(RawGithubUrl + string.Join("", segs));
            }
            // Download text from url
            string data;
            try
            {
                data = client.DownloadString(url);
            }
            catch (WebException)
            {
                return null;
            }
            // Read config from text
            conf = configReader.From(data);
            cached.Add(dependency, conf);
            return conf;
        }

        public void ResolveDependency(in Config myConfig, in Dependency dependency)
        {
            if (!cached.TryGetValue(dependency, out var config))
                config = GetConfig(dependency);

            var url = config.Info.Url;
            if (config.Info.Url is null)
                // Fallback to dependency url and additional info
                url = dependency.Url;

            if (IsGithubLink(url))
            {
                // If we have a github link, we need to create an archive download link
                // branch is always determined from dependency AdditionalData
                if (!dependency.AdditionalData.TryGetValue("branchName", out var branchName))
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
        }
    }
}