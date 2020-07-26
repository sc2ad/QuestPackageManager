using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestPackageManager.Data
{
    public class Dependency
    {
        public string? Id { get; set; }

        [JsonConverter(typeof(SemVerRangeConverter))]
        public SemVer.Range? VersionRange { get; set; }

        [JsonInclude]
        public Dictionary<string, JsonElement> AdditionalData { get; private set; } = new Dictionary<string, JsonElement>();

        public Dependency(string id, SemVer.Range versionRange)
        {
            Id = id;
            VersionRange = versionRange;
        }

        [JsonConstructor]
        private Dependency()
        {
        }
    }
}