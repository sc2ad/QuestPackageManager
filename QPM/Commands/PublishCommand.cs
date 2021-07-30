using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("publish", Description = "Publish package")]
    internal class PublishCommand
    {
        private async Task OnExecute()
        {
            var res = await Program.PublishHandler.Publish().ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
                Utils.WriteSuccess();
            else
                Utils.WriteFail($"Failed! Status code: {res.StatusCode} ({(int)res.StatusCode})");
        }
    }
}