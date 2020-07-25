using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QPM.Data
{
    public class Configuration
    {
        [JsonPropertyName("defines")]
        public List<string> Defines { get; set; }

        [JsonPropertyName("includePath")]
        public List<string> IncludePath { get; set; } = new List<string>();

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }

    public class CppProperties
    {
        [JsonPropertyName("configurations")]
        public List<Configuration> Configurations { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }

        private const string IdDefine = "ID";
        private const string VersionDefine = "VERSION";

        public CppProperties()
        {
        }

        public void AddIncludePath(string toAdd)
        {
            var config = Configurations.FirstOrDefault();
            if (config is null)
                return;
            var existing = config.IncludePath.FindIndex(s => s == toAdd);
            if (existing == -1)
                config.IncludePath.Add(toAdd);
        }

        public void UpdateId(string id)
        {
            var config = Configurations.FirstOrDefault();
            if (config is null)
                return;
            var idDef = config.Defines.FindIndex(d => d.StartsWith(IdDefine));
            var toAdd = IdDefine + $"=\"{id}\"";
            if (idDef != -1)
                config.Defines[idDef] = toAdd;
            else
                config.Defines.Add(toAdd);
        }

        public void UpdateVersion(SemVer.Version version)
        {
            var config = Configurations.FirstOrDefault();
            if (config is null)
                return;
            var versionDef = config.Defines.FindIndex(d => d.StartsWith(VersionDefine));
            var toAdd = IdDefine + $"=\"{version}\"";
            if (versionDef != -1)
                config.Defines[versionDef] = toAdd;
            else
                config.Defines.Add(toAdd);
        }
    }
}