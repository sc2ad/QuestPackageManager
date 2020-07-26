using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    public class Config
    {
        public string SharedDir { get; set; } = "shared";
        public string DependenciesDir { get; set; } = "extern";
        public PackageInfo? Info { get; set; }

        [JsonInclude]
        public List<Dependency> Dependencies { get; private set; } = new List<Dependency>();

        [JsonInclude]
        public Dictionary<string, JsonElement> AdditionalData { get; private set; } = new Dictionary<string, JsonElement>();
    }
}