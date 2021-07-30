using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("collapse", Description = "Collect and collapse dependencies and print them to console")]
    internal class CollapseCommand
    {
        private string PrintRestoredDependency(RestoredDependencyPair pair) => $"{pair.Dependency.Id}: ({pair.Dependency.VersionRange}) --> {pair.Version}";

        private void PrintDependencies(string indent, SharedConfig config)
        {
            foreach (var p in config.RestoredDependencies)
            {
                Console.WriteLine(indent + PrintRestoredDependency(p));
                // TODO: Recurse down this properly
            }
        }

        private async Task OnExecute()
        {
            var outp = await Program.RestoreHandler.CollectDependencies().ConfigureAwait(false);
            var collapsed = RestoreHandler.CollapseDependencies(outp);
            foreach (var pair in collapsed)
            {
                Console.WriteLine($"{PrintRestoredDependency(pair.Key)} (config: {pair.Value.Config.Info.Version}, {pair.Value.RestoredDependencies.Count} restored dependencies)");
                PrintDependencies("- ", pair.Value);
            }
            Utils.WriteSuccess();
        }
    }
}