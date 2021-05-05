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
        private readonly ISymLinkCreator? _linker;

        /// <summary>
        /// Creates a new Symbolic Link Creator
        /// </summary>
        public Linker()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _linker = new WindowsSymLinkCreator();
            }
            // TODO: Implement
            // else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            // {
            //     _linker = new LinuxSymLinkCreator();
            // }
            // else
            // {
            //     _linker = new OSXSymLinkCreator();
            // }
        }

        public bool IsValid() => _linker is not null;

        /// <summary>
        /// Creates a symbolic link from <paramref name="source"/> to <paramref name="dest"/>
        /// </summary>
        /// <param name="source">Source file</param>
        /// <param name="dest">Destination directory</param>
        /// <returns>
        /// Returns true if the system was able to create the SymLink
        /// </returns>
        public string? CreateLink(string source, string dest)
        {
            if (!IsValid())
            {
                return "Platform does not support symlinking or hard linking yet";
            }

            var error = CheckLinkReadiness(source, dest);

            if (error != null)
            {
                return error;
            }

            var linkMade = _linker.CreateSymLink(source, dest, true);

            return !linkMade ? "Failed to create link" : null;
        }

        /// <summary>
        /// Checks for readiness of a drive to perform a SymLink creation. Expects absolute path
        /// </summary>
        /// <param name="source">Source file path</param>
        /// <param name="dest">Destination directory path</param>
        /// <returns>
        /// Returns true if the system is ready to perform a SymLink
        /// </returns>
        private string? CheckLinkReadiness(string source, string dest)
        {
            // Check existance
            if (!File.Exists(source))
                return "File source not found";


            // Escape file to directory
            if (Path.HasExtension(dest) && !Directory.Exists(dest))
                dest = Path.GetDirectoryName(dest) ?? string.Empty;

            return !Directory.Exists(dest) ? "Folder destination not found" : null;
        }
    }
}
