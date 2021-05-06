using SymLinker;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SymLinker.LinkCreators
{
    internal class WindowsSymLinkCreator : ISymLinkCreator
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode )]
        static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode )]
        static extern bool CreateSymbolicLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        public bool CreateSymLink(string source, string dest, bool file)
        {
            var symbolicLinkType = file ? SymbolicLink.File : SymbolicLink.Directory;
            switch (symbolicLinkType)
            {
                case SymbolicLink.File:
                    return CreateHardLink(dest, source, IntPtr.Zero);
                // TODO: This doesn't work, we'll need to work on it
                case SymbolicLink.Directory:
                    return CreateSymbolicLink(dest, source, (IntPtr) symbolicLinkType);
            }

            return false;
        }

        private enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

    }
}
