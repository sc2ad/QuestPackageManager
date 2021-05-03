using System;
using System.Collections.Generic;
using System.Text;

namespace SymLinker
{
    interface ISymLinkCreator
    {
        bool CreateSymLink(string linkPath, string targetPath, bool file);
    }
}
