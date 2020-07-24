using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    [Command("restore", Description = "Restore and resolve all dependencies from the package")]
    internal class RestoreCommand
    {
        private void OnExecute()
        {
            // Call restore handler restore
        }
    }
}