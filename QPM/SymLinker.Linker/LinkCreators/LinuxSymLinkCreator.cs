using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker.Linker.LinkCreators
{
    internal class LinuxSymLinkCreator : ISymLinkCreator
    {
        public bool CreateSymLink(string linkPath, string targetPath, bool file)
        {
            throw new NotImplementedException("LinuxSymLinkCreator");
        }
    }
}
