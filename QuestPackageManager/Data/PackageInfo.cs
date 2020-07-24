using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuestPackageManager.Data
{
    public class PackageInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }

        [JsonConverter(typeof(SemVerConverter))]
        public SemVer.Version Version { get; set; }

        public Uri? Url { get; set; }

        [JsonInclude]
        public Dictionary<string, string?> AdditionalData { get; private set; } = new Dictionary<string, string?>();

        public PackageInfo(string name, string id, SemVer.Version version)
        {
            Name = name;
            Id = id;
            Version = version;
        }
    }
}