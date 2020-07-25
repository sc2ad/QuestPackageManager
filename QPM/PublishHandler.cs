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

        public PublishHandler(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
        }

        public void Publish()
        {
            // Ensure the config is valid
            // Push it to the server
        }
    }
}