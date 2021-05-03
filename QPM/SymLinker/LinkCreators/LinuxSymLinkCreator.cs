using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker.LinkCreators
{
    internal class LinuxSymLinkCreator : ISymLinkCreator
    {
        internal LinuxSymLinkCreator() => throw new NotImplementedException("LinuxSymLinkCreator");

        public bool CreateSymLink(string linkPath, string targetPath, bool file) => throw new NotImplementedException("LinuxSymLinkCreator");
    }
}
