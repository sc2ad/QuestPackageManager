using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM
{
    internal class LocalConfigProvider : IConfigProvider
    {
        private readonly string path;

        private Config config;
        private bool configGotten;

        private JsonSerializerOptions options;

        public LocalConfigProvider(string dir, string fileName)
        {
            path = Path.Combine(dir, fileName);
            options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
        }

        public void Commit()
        {
            if (config is null)
                throw new InvalidOperationException("Cannot commit config when it is null!");
            var str = JsonSerializer.Serialize(config, options);
            File.WriteAllText(path, str);
        }

        public Config GetConfig(bool createOnFail = false)
        {
            if (configGotten)
                return config;
            configGotten = true;
            config = null;
            if (!File.Exists(path))
            {
                if (createOnFail)
                {
                    config = new Config();
                    // Commit the created config when we explicitly want to create on failure
                    Commit();
                }
            }
            else
            {
                // These will throw as needed to the caller on failure
                // TODO: If we can solve the issue by recreating the JSON, we can try that here
                var json = File.ReadAllText(path);
                config = JsonSerializer.Deserialize<Config>(json, options);
            }
            return config;
        }
    }
}