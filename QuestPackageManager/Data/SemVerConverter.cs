using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    public class SemVerConverter : JsonConverter<SemVer.Version>
    {
        [return: MaybeNull]
        public override SemVer.Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new SemVer.Version(reader.GetString());

        public override void Write(Utf8JsonWriter writer, SemVer.Version value, JsonSerializerOptions options) => writer?.WriteStringValue(value?.ToString());
    }

    public class SemVerRangeConverter : JsonConverter<SemVer.Range>
    {
        [return: MaybeNull]
        public override SemVer.Range Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new SemVer.Range(reader.GetString());

        public override void Write(Utf8JsonWriter writer, SemVer.Range value, JsonSerializerOptions options) => writer?.WriteStringValue(value?.ToString());
    }
}