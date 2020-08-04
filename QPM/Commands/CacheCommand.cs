using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("cache", Description = "Cache control")]
    [Subcommand(typeof(CacheClear))]
    internal class CacheCommand
    {
        [Command("clear", Description = "Clear the cache")]
        internal class CacheClear
        {
            private void OnExecute()
            {
                Utils.DeleteTempDir();
                Utils.WriteSuccess();
            }
        }
    }
}