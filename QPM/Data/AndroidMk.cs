using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Data
{
    public class AndroidMk
    {
        public List<string> Prefix { get; } = new List<string>();
        public List<Module> Modules { get; } = new List<Module>();
        public List<string> Suffix { get; } = new List<string>();
    }
}