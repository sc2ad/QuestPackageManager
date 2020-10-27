using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("restore", Description = "Restore and resolve all dependencies from the package")]
    internal class RestoreCommand
    {
        private void OnExecute()
        {
            var config = Program.configProvider.GetConfig();
            if (config is null)
                throw new ConfigException("Cannot restore without a valid QPM package! Try 'qpm package create' first.");
            var sharedConfig = Program.configProvider.GetSharedConfig();
            Utils.CreateDirectory(Path.Combine(Environment.CurrentDirectory, config.SharedDir));
            Utils.CreateDirectory(Path.Combine(Environment.CurrentDirectory, config.DependenciesDir));
            // We want to delete any existing libs that we no longer need
            var deps = Program.RestoreHandler.CollectDependencies();
            // Holds all .so files that have been mapped to. False indicates the file should be deleted.
            var existingDeps = Directory.EnumerateFiles(config.DependenciesDir).ToDictionary(s => s, s => false);
            foreach (var d in deps)
            {
                var name = d.Value.Config.Info.GetSoName(out var overrodenName);
                if (name != null)
                {
                    var path = Path.Combine(config.DependenciesDir, name);
                    if (File.Exists(path))
                    {
                        // If it's overroden, do nothing, it gets deleted by itself.
                        // Actually, if it's an overriden name, we should check qpm.shared.json
                        // If we find that we already have a match, we don't need to remove it
                        if (sharedConfig != null)
                        {
                            var found = sharedConfig.RestoredDependencies.Find(rdp => rdp.Dependency.Id.Equals(d.Value.Config.Info.Id, StringComparison.OrdinalIgnoreCase) && rdp.Version == d.Value.Config.Info.Version);
                            if (found != null)
                            {
                                // If we have a match of the same version, we are chillin
                                // Otherwise, we need to delete this.
                                existingDeps[path] = true;
                            }
                        }
                        // If it isn't, set the flag to true
                        if (existingDeps.ContainsKey(path))
                        {
                            existingDeps[path] = true;
                        }
                    }
                }
            }
            foreach (var p in existingDeps)
            {
                // Delete each pair with a false value
                if (!p.Value)
                    File.Delete(p.Key);
            }
            Program.RestoreHandler.Restore();
            // Write Android.mk
            Utils.WriteSuccess();
        }
    }
}