using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QPM
{
    public class ModPair
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        [JsonConverter(typeof(SemVerConverter))]
        public SemVer.Version Version { get; set; }
    }

    public sealed class QPMApi
    {
        private readonly IConfigProvider configProvider;
        private const string ApiUrl = "https://qpackages.com";
        private const string AuthorizationHeader = "not that i can come up with";

        private readonly WebClient client;

        public QPMApi(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
            client = new WebClient
            {
                BaseAddress = ApiUrl
            };
        }

        public List<string> GetAllPackages()
        {
            var s = client.DownloadString("/");
            return JsonSerializer.Deserialize<List<string>>(s);
        }

        public List<ModPair> GetAll(string id, uint limit = 0)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (limit == 1)
                limit = 0;
            var s = client.DownloadString($"/{id}/?req=*&limit={limit}");
            return JsonSerializer.Deserialize<List<ModPair>>(s);
        }

        public ModPair GetLatest(string id, SemVer.Range range = null)
        {
            if (range is null)
                range = new SemVer.Range("*");
            if (string.IsNullOrEmpty(id))
                return null;
            // We need to perform a double check to make sure we format this string properly for the backend.
            // It's horrible and sad, but it must be done.

            var rangeStr = range.ToString();
            var ind = rangeStr.IndexOf('<');
            if (ind > 1)
            {
                rangeStr = rangeStr.Substring(0, ind) + "," + rangeStr.Substring(ind);
            }
            else
            {
                ind = rangeStr.IndexOf('>');
                if (ind > 1)
                {
                    rangeStr = rangeStr.Substring(0, ind) + "," + rangeStr.Substring(ind);
                }
            }
            var s = client.DownloadString($"/{id}?req={rangeStr}");
            return JsonSerializer.Deserialize<ModPair>(s);
        }

        public ModPair GetLatest(Dependency d, SemVer.Version specific = null) => specific != null ? GetLatest(d.Id, new SemVer.Range("=" + specific)) : GetLatest(d.Id, d.VersionRange);

        public SharedConfig GetLatestConfig(Dependency d, SemVer.Version specific = null)
        {
            var pair = GetLatest(d, specific);
            return GetConfig(pair.Id, pair.Version);
        }

        public SharedConfig GetConfig(string id, SemVer.Version version)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (version is null)
                return null;
            var s = client.DownloadString($"/{id}/{version}");
            return configProvider.From(s);
        }

        public void Push(SharedConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            // We don't perform any validity here, simply ship it away
            var s = configProvider.ToString(config);
            client.Headers.Add(HttpRequestHeader.Authorization, AuthorizationHeader);
            client.UploadString($"/{config.Config.Info.Id}/{config.Config.Info.Version}", s);
        }
    }
}