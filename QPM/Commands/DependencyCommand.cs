using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("dependency", Description = "Dependency control")]
    [Subcommand(typeof(DependencyAdd), typeof(DependencyRemove))]
    internal class DependencyCommand
    {
        [Command("add", Description = "Add a new dependency")]
        internal class DependencyAdd
        {
            [Argument(0, "id", Description = "Id of the dependency")]
            [Required]
            public string Id { get; }

            [Option("-v|--version", CommandOptionType.SingleValue, Description = "Version range to use for the dependency. Defaults to \"*\"")]
            public string Version { get; }

            [Argument(2, "additional info", Description = "Additional information for the dependency (as a valid json object)")]
            public string AdditionalInfo { get; }

            private void OnExecute()
            {
                if (string.IsNullOrEmpty(Id))
                    throw new ArgumentException("Id for 'dependency add' cannot be null or empty!");
                // Create range for dependency
                // Will throw on failure
                var range = new SemVer.Range(string.IsNullOrEmpty(Version) ? "*" : Version);
                var dep = new Dependency(Id, range);
                // Populate AdditionalInfo
                if (AdditionalInfo != null)
                {
                    using var doc = JsonDocument.Parse(AdditionalInfo);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        throw new ArgumentException("AdditionalData must be a JSON object!");
                    foreach (var p in doc.RootElement.EnumerateObject())
                        dep.AdditionalData.Add(p.Name, p.Value);
                }
                // Call dependency handler add
                Program.DependencyHandler.AddDependency(dep);
                Console.WriteLine($"Added dependency: {Id} ok!");
                Utils.WriteSuccess();
            }
        }

        [Command("remove", Description = "Remove an existing dependency")]
        internal class DependencyRemove
        {
            [Argument(0, "id", Description = "Id to remove")]
            [Required]
            public string Id { get; }

            private void OnExecute()
            {
                if (string.IsNullOrEmpty(Id))
                    throw new ArgumentException("Id for 'dependency remove' cannot be null or empty!");
                // Call dependency handler remove
                if (!Program.DependencyHandler.RemoveDependency(Id))
                    throw new InvalidOperationException($"Cannot remove id: {Id} because it is not a dependency!");
                Console.WriteLine($"Removed dependency: {Id} ok!");
                Utils.WriteSuccess();
            }
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}