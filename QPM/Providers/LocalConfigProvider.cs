using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM.Providers
{
    internal class LocalConfigProvider : IConfigProvider
    {
        private readonly string configPath;
        private readonly string localConfigPath;

        private Config config;
        private SharedConfig localConfig;
        private bool configGotten;
        private bool localConfigGotten;

        private JsonSerializerOptions options;

        public LocalConfigProvider(string dir, string configFileName, string localFileName)
        {
            configPath = Path.Combine(dir, configFileName);
            localConfigPath = Path.Combine(dir, localFileName);
            options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        public string ToString(SharedConfig config)
        {
            try
            {
                return JsonSerializer.Serialize(config, options);
            }
            catch
            {
                return null;
            }
        }

        public SharedConfig From(string data)
        {
            try
            {
                return JsonSerializer.Deserialize<SharedConfig>(data, options);
            }
            catch
            {
                return null;
            }
        }

        public void Commit()
        {
            if (config is null && localConfig is null)
                throw new InvalidOperationException("Cannot commit config or localConfig when both are null!");
            if (config != null)
            {
                var str = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, str);
            }
            if (localConfig != null)
            {
                var str = JsonSerializer.Serialize(localConfig, options);
                File.WriteAllText(localConfigPath, str);
            }
        }

        public SharedConfig GetSharedConfig(bool createOnFail = false)
        {
            if (localConfigGotten)
                return localConfig;
            localConfigGotten = true;
            localConfig = null;
            if (!File.Exists(localConfigPath))
            {
                if (createOnFail)
                {
                    localConfig = new SharedConfig();
                    Console.WriteLine("Creating new local config at: " + localConfigPath);
                    // Commit the created config when we explicitly want to create on failure
                    Commit();
                }
            }
            else
            {
                // These will throw as needed to the caller on failure
                // TODO: If we can solve the issue by recreating the JSON, we can try that here
                var json = File.ReadAllText(localConfigPath);
                localConfig = JsonSerializer.Deserialize<SharedConfig>(json, options);
            }
            return localConfig;
        }

        public Config GetConfig(bool createOnFail = false)
        {
            if (configGotten)
                return config;
            configGotten = true;
            config = null;
            if (!File.Exists(configPath))
            {
                if (createOnFail)
                {
                    config = new Config();
                    Console.WriteLine("Creating new config at: " + configPath);
                    // Commit the created config when we explicitly want to create on failure
                    Commit();
                }
            }
            else
            {
                // These will throw as needed to the caller on failure
                // TODO: If we can solve the issue by recreating the JSON, we can try that here
                var json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<Config>(json, options);
            }
            return config;
        }
    }
}