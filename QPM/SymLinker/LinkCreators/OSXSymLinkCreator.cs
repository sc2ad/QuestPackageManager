using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker.LinkCreators
{
    internal class OSXSymLinkCreator : ISymLinkCreator
    {
        internal OSXSymLinkCreator() => throw new NotImplementedException("OSXSymLinkCreator");

        public bool CreateSymLink(string linkPath, string targetPath, bool file) => throw new NotImplementedException("OSXSymLinkCreator");
    }
}
