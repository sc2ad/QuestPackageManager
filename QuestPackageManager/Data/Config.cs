using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    public class Config
    {
        public string SharedDir { get; set; } = "shared";
        public string DependenciesDir { get; set; } = "extern";
        public PackageInfo? Info { get; set; }

        public List<Dependency> Dependencies { get; } = new List<Dependency>();

        public List<Dependency> IncludedDependencies { get; } = new List<Dependency>();

        public Dictionary<string, string?> AdditionalData { get; } = new Dictionary<string, string?>();
    }
}