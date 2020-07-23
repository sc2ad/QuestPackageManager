using System;
using System.Collections.Generic;

namespace QuestPackageManager.Data
{
    public class PackageInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public SemVer.Version Version { get; set; }
        public Uri? Url { get; set; }
        public Dictionary<string, string?> AdditionalData { get; } = new Dictionary<string, string?>();

        public PackageInfo(string name, string id, SemVer.Version version)
        {
            Name = name;
            Id = id;
            Version = version;
        }
    }
}