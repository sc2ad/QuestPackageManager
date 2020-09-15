using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    public class RestoredDependencyPair : IEquatable<RestoredDependencyPair>
    {
        public Dependency? Dependency { get; set; }

        [JsonConverter(typeof(SemVerConverter))]
        public SemVer.Version? Version { get; set; }

        public static bool operator ==(RestoredDependencyPair? left, RestoredDependencyPair? right) => (left?.Equals(right)) ?? false;

        public static bool operator !=(RestoredDependencyPair? left, RestoredDependencyPair? right) => (left?.Equals(right)) ?? true;

        public override bool Equals(object? obj) => obj is RestoredDependencyPair d ? Equals(d) : false;

        public bool Equals([AllowNull] RestoredDependencyPair other) => other?.Dependency == Dependency && other?.Version == Version;

        public override int GetHashCode() => (Dependency?.GetHashCode() + 59 * Version?.GetHashCode()).GetValueOrDefault();
    }
}