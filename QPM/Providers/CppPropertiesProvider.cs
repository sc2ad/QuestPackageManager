using QPM.Data;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QPM.Providers
{
    public class CppPropertiesProvider
    {
        private readonly string path;

        public CppPropertiesProvider(string path)
        {
            this.path = path;
        }

        public void SerializeProperties(CppProperties props)
        {
            // Throws
            var data = JsonSerializer.Serialize(props);
            // Throws
            File.WriteAllText(path, data);
        }

        public CppProperties GetProperties()
        {
            try
            {
                var data = File.ReadAllText(path);
                return JsonSerializer.Deserialize<CppProperties>(data);
            }
            catch
            {
                // On failure to read properties, return null or throw
                return null;
            }
        }
    }
}