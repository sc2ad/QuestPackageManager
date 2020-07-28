using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("properties-list", Description = "List all properties that are currently supported by QPM")]
    internal class SupportedPropertiesCommand
    {
        internal class PropertyAttribute : Attribute
        {
            public string[] SupportedTypes { get; set; }
            public string HelpText { get; set; }
            public string Type { get; set; } = "string";

            public PropertyAttribute(string helpText, params string[] types)
            {
                HelpText = helpText;
                SupportedTypes = types;
            }
        }

        [Property("Branch name of a Github repo. Only used when a valid github url is provided", "package", "dependency")]
        public const string BranchName = "branchName";

        [Property("Specify that this package is headers only and does not contain a .so file", "package", Type = "bool")]
        public const string HeadersOnly = "headersOnly";

        [Property("Specify the download link for a release .so file", "package")]
        public const string ReleaseSoLink = "soLink";

        [Property("Specify any additional files to be downloaded", "package", "dependency", Type = "array[string]")]
        public const string AdditionalFiles = "extraFiles";

        [Property("Copy a dependency from a location that is local to this root path instead of from a remote url", "dependency")]
        public const string LocalPath = "localPath";

        [Property("Specify the download link for a debug .so files (usually from the obj folder)", "package")]
        public const string DebugSoLink = "debugSoLink";

        [Property("Specify if a dependency should download a release .so file. Defaults to false", "dependency", Type = "bool")]
        public const string UseReleaseSo = "useRelease";

        [Property("Override the downloaded .so filename with this name instead.", "package")]
        public const string OverrideSoName = "overrideSoName";

        private const string Header = "Supported Additional Properties:";

        private void OnExecute()
        {
            // Aggregate const fields
            var fields = typeof(SupportedPropertiesCommand).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var sb = new StringBuilder(Header);
            sb.AppendLine();
            sb.AppendLine();
            foreach (var f in fields)
            {
                var attrs = f.GetCustomAttributes(typeof(PropertyAttribute), false);
                if (attrs.Length == 0)
                    continue;
                var attr = attrs.First() as PropertyAttribute;
                if (f.GetRawConstantValue() is string val)
                {
                    sb.AppendFormat("{0} ({1}): {2} - Supported in: {3}", val, attr.Type, attr.HelpText, string.Join(", ", attr.SupportedTypes));
                    sb.AppendLine();
                }
            }
            Console.WriteLine(sb.ToString());
        }
    }
}