using SymLinker.Linker;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SymLinker.Linker.LinkCreators
{
    internal class WindowsSymLinkCreator : ISymLinkCreator
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode )]
        static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        public bool CreateSymLink(string source, string dest, bool file)
        {
            var symbolicLinkType = file ? SymbolicLink.File : SymbolicLink.Directory;
            return CreateHardLink(dest, source, (IntPtr) symbolicLinkType);
        }

        private enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

    }
}
