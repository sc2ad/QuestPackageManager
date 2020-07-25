using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QPM.Data
{
    public class BmbfMod
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        [JsonConverter(typeof(SemVerConverter))]
        public SemVer.Version Version { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }

        public void UpdateId(string id)
        {
            if (!string.IsNullOrEmpty(id))
                Id = id;
        }

        public void UpdateName(string name)
        {
            if (!string.IsNullOrEmpty(name))
                Name = name;
        }

        public void UpdateVersion(SemVer.Version version)
        {
            if (version != null)
                Version = version;
        }
    }
}