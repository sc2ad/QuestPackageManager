using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestPackageManager.Data
{
    public class Dependency : IEquatable<Dependency>
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

        public bool Equals([AllowNull] Dependency other)
        {
            if (other is null)
                return false;
            return Id == other.Id
                && VersionRange == other.VersionRange
                && AdditionalData.Count == other.AdditionalData.Count
                && !AdditionalData.Keys.Any(k => !other.AdditionalData.ContainsKey(k))
                && !other.AdditionalData.Keys.Any(k => !AdditionalData.ContainsKey(k));
        }

        public static bool operator ==(Dependency? left, Dependency? right) => (left?.Equals(right)) ?? false;

        public static bool operator !=(Dependency? left, Dependency? right) => (left?.Equals(right)) ?? true;

        public override bool Equals(object? obj) => Equals(obj as Dependency);

        public override int GetHashCode() => string.GetHashCode(Id, StringComparison.OrdinalIgnoreCase) * 19 + VersionRange?.GetHashCode() * 59 + AdditionalData.Count ?? 0;
    }
}