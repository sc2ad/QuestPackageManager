using QPM.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM.Providers
{
    public class BmbfModProvider
    {
        private readonly string path;

        public BmbfModProvider(string path)
        {
            this.path = path;
        }

        public void SerializeMod(BmbfMod mod)
        {
            // Throws
            var data = JsonSerializer.Serialize(mod);
            // Throws
            File.WriteAllText(path, data);
        }

        public BmbfMod GetMod()
        {
            try
            {
                var data = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BmbfMod>(data);
            }
            catch
            {
                // On failure to read properties, return null or throw
                return null;
            }
        }
    }
}