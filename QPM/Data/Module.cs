using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Data
{
    public class Module
    {
        public List<string> PrefixLines { get; set; } = new List<string>();
        public string? Id { get; set; }
        public List<string> Src { get; set; } = new List<string>();
        public List<string> ExportIncludes { get; set; } = new();
        public List<string> StaticLibs { get; set; } = new List<string>();
        public List<string> SharedLibs { get; set; } = new List<string>();
        public List<string> LdLibs { get; set; } = new List<string>();
        public List<string> CFlags { get; set; } = new List<string>();
        public List<string> ExportCFlags { get; set; } = new List<string>();
        public List<string> ExportCppFlags { get; set; } = new();
        public List<string> CppFlags { get; set; } = new List<string>();
        public List<string> CIncludes { get; set; } = new List<string>();
        public List<string> CppFeatures { get; set; } = new List<string>();
        public List<string> ExtraLines { get; set; } = new List<string>();
        public string? BuildLine { get; set; }

        public void AddDefine(string id, string value)
        {
            // TODO: Support more types of -D
            var idIndex = CFlags.FindIndex(c => c.StartsWith("-D" + id) || c.StartsWith("-D'" + id));
            var s = $"-D{id}='\"{value}\"'";
            if (idIndex != -1)
                CFlags[idIndex] = s;
            else
                CFlags.Add(s);
        }

        public void AddIncludePath(string includePath)
        {
            var include = "-I'" + includePath + "'";
            if (!CFlags.Contains(include))
                CFlags.Add(include);
        }

        public void AddSystemInclude(string includePath, bool export = false)
        {
            var include = "-isystem'" + includePath + "'";
            var lst = export ? ExportCFlags : CFlags;
            if (!lst.Contains(include))
                lst.Add(include);
        }

        public void AddExportCppFeature(string feature)
        {
            var f = "-f" + feature;
            if (!ExportCppFlags.Contains(f))
                ExportCppFlags.Add(f);
        }

        public void EnsureIdIs(string id, SemVer.Version version) => Id = id + "_" + version.ToString().Replace('.', '_');

        public void RemoveLibrary(string id)
        {
            SharedLibs.RemoveAll(l => l.Equals(id, StringComparison.OrdinalIgnoreCase));
            StaticLibs.RemoveAll(l => l.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }
}