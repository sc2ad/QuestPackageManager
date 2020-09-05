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

        private void OnExecute()
        {
            var outp = Program.RestoreHandler.CollectDependencies();
            var collapsed = RestoreHandler.CollapseDependencies(outp);
            foreach (var pair in collapsed)
            {
                Console.WriteLine($"{pair.Key} --> {pair.Value.conf.Config.Info.Version}, {pair.Value.conf.RestoredDependencies.Count} restored dependencies");
                PrintDependencies("- ", pair.Value.conf);
            }
            Utils.WriteSuccess();
        }
    }
}