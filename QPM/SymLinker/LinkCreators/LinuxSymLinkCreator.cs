using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SymLinker.LinkCreators
{
    internal class LinuxSymLinkCreator : ISymLinkCreator
    {
        const string LIBC = "c.so";

        [DllImport(LIBC)]
        private static extern int symlink(
            string path1,
            string path2
        );

        public bool CreateSymLink(string linkPath, string targetPath, bool file)
        {
            // Will this delete the symlink or target? We'll find out soon
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath);

            return symlink(linkPath, targetPath) == 0;
        }
    }
}
