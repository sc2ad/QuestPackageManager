using SymLinker;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SymLinker.LinkCreators
{
    internal class WindowsSymLinkCreator : ISymLinkCreator
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode )]
        static extern bool CreateSymbolicLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        public bool CreateSymLink(string source, string dest, bool file)
        {
            var symbolicLinkType = file ? SymbolicLink.File : SymbolicLink.Directory;
            return CreateSymbolicLink(dest, source, (IntPtr) symbolicLinkType);
        }

        private enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

    }
}
