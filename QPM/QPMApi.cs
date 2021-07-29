using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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

        private readonly HttpClient client;

        public QPMApi(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
            client = new HttpClient
            {
                BaseAddress = new Uri(ApiUrl),
                Timeout = TimeSpan.FromSeconds(300)
            };
            client.DefaultRequestHeaders.Add("User-Agent", "QPM_" + Assembly.GetCallingAssembly().GetName().Version?.ToString());
        }

        public async Task<List<string>?> GetAllPackages()
        {
            var s = await client.GetStringAsync("/").ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<string>>(s);
        }

        public async Task<List<ModPair>?> GetAll(string id, uint limit = 0)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (limit == 1)
                limit = 0;
            var s = await client.GetStringAsync($"/{id}/?req=*&limit={limit}").ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ModPair>>(s);
        }

        public async Task<ModPair?> GetLatest(string id, SemVer.Range? range = null)
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
                rangeStr = rangeStr.Substring(0, ind) + "," + rangeStr[ind..];
            }
            else
            {
                ind = rangeStr.IndexOf('>');
                if (ind > 1)
                {
                    rangeStr = rangeStr.Substring(0, ind) + "," + rangeStr[ind..];
                }
            }
            var s = await client.GetStringAsync($"/{id}?req={rangeStr}").ConfigureAwait(false);
            return JsonSerializer.Deserialize<ModPair>(s);
        }

        public async Task<ModPair?> GetLatest(Dependency d, SemVer.Version? specific = null) => specific != null ? await GetLatest(d.Id!, new SemVer.Range("=" + specific)).ConfigureAwait(false) : await GetLatest(d.Id!, d.VersionRange).ConfigureAwait(false);

        public async Task<SharedConfig?> GetLatestConfig(Dependency d, SemVer.Version? specific = null)
        {
            var pair = await GetLatest(d, specific).ConfigureAwait(false);
            if (pair is null)
                return null;
            return await GetConfig(pair.Id, pair.Version).ConfigureAwait(false);
        }

        public async Task<SharedConfig?> GetConfig(string id, SemVer.Version version)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (version is null)
                return null;
            var s = await client.GetStringAsync($"/{id}/{version}").ConfigureAwait(false);
            return configProvider.From(s);
        }

        public async Task Push(SharedConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            // We don't perform any validity here, simply ship it away
            var s = configProvider.ToString(config);
            var request = new HttpRequestMessage(HttpMethod.Post, $"/{config.Config!.Info!.Id}/{config.Config.Info.Version}")
            {
                Content = new StringContent(s)
            };
            request.Headers.Add("Authorization", AuthorizationHeader);

            await client.SendAsync(request).ConfigureAwait(false);
        }
    }
}