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
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException("Config does not exist!");
            var localConfig = configProvider.GetLocalConfig();

            // All ids in config.Dependencies must be covered in localConfig.IncludedDependencies
            var ids = config.Dependencies.Select(d => d.Id);
            if (ids.Any())
            {
                if (localConfig is null)
                    throw new ConfigException("Local config does not exist!");
                foreach (var id in ids)
                {
                    var val = localConfig.IncludedDependencies.Find(d => id.Equals(d.Id, StringComparison.OrdinalIgnoreCase));
                    if (val is null)
                        throw new DependencyException("Not all dependencies are restored! Restore before attempting to publish!");
                }
            }
            // My shared folder should have includes that don't use ..
            // My config should have both a Url and a soUrl
            if (config.Info.Url is null)
                throw new DependencyException("Config url does not exist!");
            if (config.Info.AdditionalData.ContainsKey("soUrl"))
                throw new DependencyException("Config soUrl does not exist!");

            // Push it to the server
            api.Push(config);
        }
    }
}