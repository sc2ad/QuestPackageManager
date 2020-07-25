using QPM.Data;
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
            var data = JsonSerializer.Serialize(mod, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
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