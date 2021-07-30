using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QPM.Commands
{
    [Command("config", Description = "Config control")]
    [Subcommand(typeof(ConfigCache), typeof(ConfigTimeout), typeof(ConfigSymlinks))]
    internal class ConfigCommand
    {
        [Command("cache", Description = "Assign the cache path")]
        internal class ConfigCache
        {
            [Argument(0, "path", Description = "Path to place the QPM Cache")]
            [Required]
            public string Path { get; }

            private async Task OnExecute()
            {
                Program.Config.CachePath = Path;
                await Program.SaveConfig().ConfigureAwait(false);
            }
        }

        [Command("timeout", Description = "Assign the timeout for web requests")]
        internal class ConfigTimeout
        {
            [Argument(0, "timeout", Description = "Timeout (in seconds) for downloads from QPM API and links provided in qpm configurations")]
            [Required]
            public double Timeout { get; }

            private async Task OnExecute()
            {
                Program.Config.DependencyTimeoutSeconds = Timeout;
                await Program.SaveConfig().ConfigureAwait(false);
            }
        }

        [Command("symlink", Description = "Enable or disable symlink usage")]
        [Subcommand(typeof(ConfigSymlinksEnable), typeof(ConfigSymlinksDisable))]
        internal class ConfigSymlinks
        {
            [Command("enable", Description = "Enable symlink usage")]
            internal class ConfigSymlinksEnable
            {
                private async Task OnExecute()
                {
                    Program.Config.UseSymlinks = true;
                    await Program.SaveConfig().ConfigureAwait(false);
                }
            }

            [Command("disable", Description = "Disable symlink usage")]
            internal class ConfigSymlinksDisable
            {
                private async Task OnExecute()
                {
                    Program.Config.UseSymlinks = true;
                    await Program.SaveConfig().ConfigureAwait(false);
                }
            }

            private void OnExecute(CommandLineApplication app) => app.ShowHelp();
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}