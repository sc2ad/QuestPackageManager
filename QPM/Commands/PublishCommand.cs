using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    [Command("publish", Description = "Publish package")]
    internal class PublishCommand
    {
        private void OnExecute()
        {
            Program.PublishHandler.Publish();
            Utils.WriteSuccess();
        }
    }
}