using System;
using System.Collections.Generic;

namespace QuestPackageManager.Data
{
    public class Dependency
    {
        public string Id { get; set; }
        public SemVer.Range VersionRange { get; set; }
        public Uri Url { get; set; }
        public Dictionary<string, string?> AdditionalData { get; } = new Dictionary<string, string?>();

        public Dependency(string id, SemVer.Range range, Uri url)
        {
            Id = id;
            VersionRange = range;
            Url = url;
        }
    }
}