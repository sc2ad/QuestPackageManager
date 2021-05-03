using SymLinker.LinkCreators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

// Stolen from https://github.com/huffSamuel/SymLinker
// Cleaned up by Fern, because the old code was horrible
namespace SymLinker
{
    public class Linker
    {
        private readonly ISymLinkCreator _linker;

        /// <summary>
        /// Creates a new Symbolic Link Creator
        /// </summary>
        public Linker()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _linker = new WindowsSymLinkCreator();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _linker = new LinuxSymLinkCreator();
            }
            else
            {
                _linker = new OSXSymLinkCreator();
            }
        }

        /// <summary>
        /// Creates a symbolic link from <paramref name="source"/> to <paramref name="dest"/>
        /// </summary>
        /// <param name="source">Source file</param>
        /// <param name="dest">Destination directory</param>
        /// <returns>
        /// Returns true if the system was able to create the SymLink
        /// </returns>
        public void CreateLink(string source, string dest)
        {
            CheckLinkReadiness(source, dest);

            var linkMade = _linker.CreateSymLink(source, dest, true);

            if (!linkMade)
                throw new IOException("Failed to create link");
        }

        /// <summary>
        /// Checks for readiness of a drive to perform a SymLink creation. Expects absolute path
        /// </summary>
        /// <param name="source">Source file path</param>
        /// <param name="dest">Destination directory path</param>
        /// <returns>
        /// Returns true if the system is ready to perform a SymLink
        /// </returns>
        private void CheckLinkReadiness(string source, string dest)
        {
            // Check existance
            if (!File.Exists(source))
            {
                throw new IOException("File source not found");
            }

            // Escape file to directory
            if (Path.HasExtension(dest) && !Directory.Exists(dest))
                dest = Path.GetDirectoryName(dest) ?? string.Empty;

            if (!Directory.Exists(dest))
                throw new IOException("Folder destination not found");
        }
    }
}
