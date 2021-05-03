using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker.Linker
{
    interface ISymLinkCreator
    {
        bool CreateSymLink(string linkPath, string targetPath, bool file);
    }
}
