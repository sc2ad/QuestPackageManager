using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM
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

            [Argument(2, "additional info", Description = "Additional information for the dependency (as key, value pairs)")]
            public string[] AdditionalInfo { get; }

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
                    if (AdditionalInfo.Length % 2 != 0 || AdditionalInfo.Length < 2)
                        throw new ArgumentException("AdditionalInfo for 'dependency add' must be of an even length >= 2! (key, value pairs)");
                    for (int i = 0; i < AdditionalInfo.Length; i += 2)
                        dep.AdditionalData.Add(AdditionalInfo[i], AdditionalInfo[i + 1]);
                }
                // Call dependency handler add
                Program.DependencyHandler.AddDependency(dep);
                Console.WriteLine($"Added dependency: {Id} ok!");
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
                // NOTE that this does NOT remove it from our satisfied dependencies.
                // It probably should.
            }
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}