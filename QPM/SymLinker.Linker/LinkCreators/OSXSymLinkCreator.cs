using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker.Linker.LinkCreators
{
    internal class OSXSymLinkCreator : ISymLinkCreator
    {
        public bool CreateSymLink(string linkPath, string targetPath, bool file)
        {
            throw new NotImplementedException("OSXSymLinkCreator");
        }
    }
}
