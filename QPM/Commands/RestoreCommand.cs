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
            Utils.CreateDirectory(Path.Combine(Environment.CurrentDirectory, config.SharedDir));
            Utils.CreateDirectory(Path.Combine(Environment.CurrentDirectory, config.DependenciesDir));
            Program.RestoreHandler.Restore();
            // Write Android.mk
            Utils.WriteSuccess();
        }
    }
}