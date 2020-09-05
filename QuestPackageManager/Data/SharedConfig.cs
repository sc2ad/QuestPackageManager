using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    /// <summary>
    /// Config generated after a restore, should be uploaded to qpackages.com
    /// This config is necessary for proper dependency resolution of .so files or version specific information.
    /// </summary>
    public class SharedConfig
    {
        /// <summary>
        /// Overall configuration object
        /// </summary>
        [JsonInclude]
        public Config? Config { get; set; }

        /// <summary>
        /// Dependencies that were restored, ID, version pairs
        /// </summary>
        [JsonInclude]
        public List<RestoredDependencyPair> RestoredDependencies { get; private set; } = new List<RestoredDependencyPair>();
    }

    public class RestoredDependencyPair
    {
        public Dependency? Dependency { get; set; }

        [JsonConverter(typeof(SemVerConverter))]
        public SemVer.Version? Version { get; set; }
    }
}