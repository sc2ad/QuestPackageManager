using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    /// <summary>
    /// Exclusively local config, which is presumably .gitignore'd
    /// </summary>
    public class LocalConfig
    {
        [JsonInclude]
        public List<Dependency> IncludedDependencies { get; private set; } = new List<Dependency>();
    }
}