using QPM.Commands;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    public class PublishHandler
    {
        private readonly IConfigProvider configProvider;
        private readonly QPMApi api;

        public PublishHandler(IConfigProvider configProvider, QPMApi api)
        {
            this.configProvider = configProvider;
            this.api = api;
        }

        public void Publish()
        {
            // Ensure the config is valid
            var sharedConfig = configProvider.GetSharedConfig();
            if (sharedConfig is null)
                throw new ConfigException("Config does not exist!");

            // All ids in config.Dependencies must be covered in localConfig.IncludedDependencies
            if (sharedConfig.Config.Dependencies.Any())
            {
                foreach (var d in sharedConfig.Config.Dependencies)
                {
                    if (!sharedConfig.RestoredDependencies.Exists(p => p.Dependency!.Id.Equals(d.Id!, StringComparison.OrdinalIgnoreCase) && d.VersionRange.IsSatisfied(p.Version)))
                        throw new DependencyException($"Not all dependencies are restored or of correct versions! Restore before attempting to publish! Missing or mismatch dependency: {d.Id} with range: {d.VersionRange}");
                }
            }
            // My shared folder should have includes that don't use ..
            // My config should have both a Url and a soUrl
            if (sharedConfig.Config.Info.Url is null)
                throw new DependencyException("Config url does not exist!");
            if (!sharedConfig.Config.Info.AdditionalData.ContainsKey(SupportedPropertiesCommand.ReleaseSoLink) && (!sharedConfig.Config.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.HeadersOnly, out var header) || !header.GetBoolean()))
                throw new DependencyException($"Config {SupportedPropertiesCommand.ReleaseSoLink} does not exist! Try using {SupportedPropertiesCommand.HeadersOnly} if you do not need a .so file. See 'properties-list' for more info");

            // Push it to the server
            api.Push(sharedConfig);
        }
    }
}