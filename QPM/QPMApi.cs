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
        private const string AuthorizationHeader = "";

        private readonly WebClient client;

        public QPMApi(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
            client = new WebClient
            {
                BaseAddress = ApiUrl
            };
        }

        public List<ModPair> GetAll(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var s = client.DownloadString($"/all/{id}/*");
            return JsonSerializer.Deserialize<List<ModPair>>(s);
        }

        public ModPair GetLatest(string id, SemVer.Range range = null)
        {
            if (range is null)
                range = new SemVer.Range("*");
            if (string.IsNullOrEmpty(id))
                return null;
            var s = client.DownloadString($"/latest/{id}/{range}");
            return JsonSerializer.Deserialize<ModPair>(s);
        }

        public ModPair GetLatest(Dependency d) => GetLatest(d.Id, d.VersionRange);

        public Config GetLatestConfig(Dependency d)
        {
            var pair = GetLatest(d);
            return GetConfig(pair.Id, pair.Version);
        }

        public Config GetConfig(string id, SemVer.Version version)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (version is null)
                return null;
            var s = client.DownloadString($"/mod/{id}/{version}");
            return configProvider.From(s);
        }

        public void Push(Config config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            // We don't perform any validity here, simply ship it away
            var s = configProvider.ToString(config);
            client.Headers.Add(HttpRequestHeader.Authorization, AuthorizationHeader);
            client.UploadString($"/mod/{config.Info.Id}/{config.Info.Version}", s);
        }
    }
}