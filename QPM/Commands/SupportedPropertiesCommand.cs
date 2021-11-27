using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("properties-list", Description = "List all properties that are currently supported by QPM")]
    internal class SupportedPropertiesCommand
    {
        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
        internal sealed class PropertyAttribute : Attribute
        {
            public string[] SupportedTypes { get; set; }
            public string HelpText { get; set; }
            public Type Type { get; set; } = typeof(string);

            public PropertyAttribute(string helpText, params string[] types)
            {
                HelpText = helpText;
                SupportedTypes = types;
            }
        }

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        internal sealed class PropertyCollectionAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        internal sealed class PropertyDescriptorAttribute : Attribute
        {
            public string HelpText { get; set; }
            public bool Required { get; set; } = false;
            public PropertyDescriptorAttribute(string help)
            {
                HelpText = help;
            }
        }

        [PropertyCollection]
        internal class StyleProperty
        {
            [PropertyDescriptor("The name of the style.", Required = true)]
            public string? Name { get; set; }
            [PropertyDescriptor("The release downloadable so link for this style. Must exist if being used as release.", Required = false)]
            public string? SoLink { get; set; }
            [PropertyDescriptor("The debug downloadable so link for this style. Must exist if being used as debug.", Required = false)]
            public string? DebugSoLink { get; set; }
        }

        [PropertyCollection]
        internal class CompileOptionsProperty
        {
            [PropertyDescriptor("Additional include paths to add, relative to the extern directory.")]
            public string[] IncludePaths { get; set; } = Array.Empty<string>();
            [PropertyDescriptor("Additional system include paths to add, relative to the extern directory.")]
            public string[] SystemIncludes { get; set; } = Array.Empty<string>();
            [PropertyDescriptor("Additional C++ features to add.")]
            public string[] CppFeatures { get; set; } = Array.Empty<string>();
            [PropertyDescriptor("Additional C++ flags to add.")]
            public string[] CppFlags { get; set; } = Array.Empty<string>();
            [PropertyDescriptor("Additional C flags to add.")]
            public string[] CFlags { get; set; } = Array.Empty<string>();
        }

        [Property("Branch name of a Github repo. Only used when a valid github url is provided", "package", "dependency")]
        public const string BranchName = "branchName";

        [Property("Specify that this package is headers only and does not contain a .so or .a file", "package", Type = typeof(bool))]
        public const string HeadersOnly = "headersOnly";

        [Property("Specify that this package is static linking", "package", Type = typeof(bool))]
        public const string StaticLinking = "staticLinking";

        [Property("Specify the download link for a release .so or .a file", "package")]
        public const string ReleaseSoLink = "soLink";

        [Property("Specify any additional files to be downloaded", "package", "dependency", Type = typeof(string[]))]
        public const string AdditionalFiles = "extraFiles";

        [Property("Copy a dependency from a location that is local to this root path instead of from a remote url", "dependency")]
        public const string LocalPath = "localPath";

        [Property("Specify the download link for a debug .so or .a files (usually from the obj folder)", "package")]
        public const string DebugSoLink = "debugSoLink";

        [Property("Specify if a dependency should download a release .so or .a file. Defaults to false", "dependency", Type = typeof(bool))]
        public const string UseReleaseSo = "useRelease";

        [Property("Override the downloaded .so or .a filename with this name instead.", "package")]
        public const string OverrideSoName = "overrideSoName";

        [Property("Provide various download links of differing styles. Styles are appended to module names.", "package", Type = typeof(StyleProperty[]))]
        public const string Styles = "styles";

        [Property("Specify the style to use.", "dependency")]
        public const string StyleToUse = "style";

        [Property("Subfolder for this particular package in the provided repository, relative to root of the repo.", "package")]
        public const string Subfolder = "subfolder";

        [Property("Additional options for compilation and edits to compilation related files.", "package", Type = typeof(CompileOptionsProperty))]
        public const string CompileOptions = "compileOptions";
        //[Property("Explicit include paths to add for this particular package to all dependents, relative to workspace root.", "package", Type = "array[string]")]
        //public const string IncludePaths = "includePaths";

        //[Property("Explicit system include paths to add for this particular package to all dependents, relative to workspace root.", "package", Type = "array[string]")]
        //public const string SystemIncludes = "systemIncludes";

        private const string Header = "Supported Additional Properties:";

        private const int typeIndent = 2;

        private void OnExecute()
        {
            // Aggregate const fields
            var fields = typeof(SupportedPropertiesCommand).GetFields(BindingFlags.Public | BindingFlags.Static);
            var sb = new StringBuilder(Header);
            sb.AppendLine();
            sb.AppendLine();
            var toWrite = new List<Type>();
            foreach (var f in fields)
            {
                var attr = f.GetCustomAttribute<PropertyAttribute>(false);
                if (attr is null)
                    continue;
                if (f.GetRawConstantValue() is string val)
                {
                    sb.AppendFormat("- {0} ({1}): {2} - Supported in: {3}", val, attr.Type, attr.HelpText, string.Join(", ", attr.SupportedTypes));
                    sb.AppendLine();
                }
                if (attr.Type.GetCustomAttribute<PropertyCollectionAttribute>() is not null)
                    toWrite.Add(attr.Type);
                else if (attr.Type.GetElementType()?.GetCustomAttribute<PropertyCollectionAttribute>() is not null)
                    toWrite.Add(attr.Type.GetElementType()!);
            }
            void WriteType(StringBuilder sb, Type type)
            {
                sb.Append("Type: ");
                sb.AppendLine(type.ToString());
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    // Indent
                    for (int i = 0; i < typeIndent; i++)
                    {
                        sb.Append(' ');
                    }
                    sb.Append("- ");
                    sb.Append(char.ToLowerInvariant(prop.Name[0]));
                    var attr = prop.GetCustomAttribute<PropertyDescriptorAttribute>();
                    if (attr is not null)
                    {
                        sb.AppendFormat("{0} - {1} ({2}): {3}", prop.Name[1..], attr.Required ? "REQUIRED" : "OPTIONAL", prop.PropertyType, attr.HelpText);
                    } else
                    {
                        sb.AppendFormat("{0} ({1})", prop.Name[1..], prop.PropertyType);
                    }
                    sb.AppendLine();
                    if (prop.PropertyType.GetCustomAttribute<PropertyCollectionAttribute>() is not null)
                        toWrite.Add(prop.PropertyType);
                    else if (prop.PropertyType.GetElementType()?.GetCustomAttribute<PropertyCollectionAttribute>() is not null)
                        toWrite.Add(prop.PropertyType.GetElementType()!);
                }
            }
            for (int i = 0; i < toWrite.Count; i++)
            {
                // If the type we are has an attribute attached (or the element type) then we need to perform a type writeout
                WriteType(sb, toWrite[i]);
            }
            Console.WriteLine(sb.ToString());
        }

        private static readonly JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true };

        internal static T? Convert<T>(JsonElement elem) => JsonSerializer.Deserialize<T>(elem.GetRawText(), options);
    }
}