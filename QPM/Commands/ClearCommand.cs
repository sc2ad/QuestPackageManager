using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("clear", Description = "Clear all resolved dependencies by clearing the lock file")]
    internal class ClearCommand
    {
        private void OnExecute()
        {
            var fname = Path.Combine(Environment.CurrentDirectory, Program.LocalFileName);
            if (File.Exists(fname))
                File.Delete(fname);
            // Also delete config.DependenciesDir
            var cfg = Program.configProvider.GetConfig(false);
            if (cfg is null)
                throw new Exception("Cannot clear because there is no valid package in this directory! Have you called 'qpm package create'?");
            Utils.DeleteDirectory(Path.Combine(Environment.CurrentDirectory, cfg.DependenciesDir));
            Utils.WriteSuccess();
        }
    }
}