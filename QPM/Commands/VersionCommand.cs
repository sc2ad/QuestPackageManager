using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("version", Description = "List the current version of QPM")]
    internal class VersionCommand
    {
        private void OnExecute() => Console.WriteLine("Quest Package Manager (QPM) Version: v" + Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
    }
}