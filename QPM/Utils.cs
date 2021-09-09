using QPM.Commands;
using QuestPackageManager.Data;
using SymLinker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    public static class Utils
    {
        private static readonly Linker linker = new();

        public static void WriteMessage(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteSuccess(string message = "Success!") => WriteMessage(message, ConsoleColor.Green);

        public static void WriteFail(string message = "Failed!") => WriteMessage(message, ConsoleColor.Red);

        public static void DirectoryPermissions(string absPath)
        {
            ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C chmod +rw --recursive \"" + absPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
                : new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"chmod -R +rw '" + absPath + "'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            var proc = Process.Start(startInfo);
            proc?.WaitForExit(1000);
        }

        public static void CreateDirectory(string path)
        {
            var info = Directory.CreateDirectory(path);
            if (info.Attributes.HasFlag(FileAttributes.ReadOnly))
                info.Attributes &= ~FileAttributes.ReadOnly;
        }

        public static byte[]? FolderHash(string path)
        {
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).OrderBy(p => p).ToList();

            using var md5 = MD5.Create();

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];

                // hash path
                string relativePath = file[(path.Length + 1)..];
                byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash contents
                byte[] contentBytes = File.ReadAllBytes(file);
                if (i == files.Count - 1)
                    md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
                else
                    md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            }

            return md5.Hash;
        }

        public static byte[] FileHash(string path)
        {
            MD5 md5 = MD5.Create();

            return md5.ComputeHash(File.ReadAllBytes(path));
        }

        public static void DeleteDirectory(string path)
        {
            if (new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Does not recurse through reparse point
                Directory.Delete(path, true);
                return;
            }
            foreach (string directory in Directory.GetDirectories(path))
                DeleteDirectory(directory);
            foreach (string file in Directory.GetFiles(path))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            _ = new DirectoryInfo(path)
            {
                Attributes = FileAttributes.Normal
            };
            try
            {
                Directory.Delete(path);
            }
            catch (IOException)
            {
                Directory.Delete(path);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <returns>true if symlinked false if copied</returns>
        public static bool SymlinkOrCopyFile(string source, string dest)
        {
            if (File.Exists(dest))
                File.Delete(dest);
            if (!Program.Config.UseSymlinks)
            {
                File.Copy(source, dest);
                return false;
            }

            if (!linker.IsValid())
            {
                Console.Error.WriteLine($"Unable to use symlinks on {RuntimeInformation.OSDescription}, falling back to copy");
                File.Copy(source, dest);
                return false;
            }

            // Attempt to make symlinks to avoid unnecessary copy
            var error = linker.CreateLink(Path.GetFullPath(source), Path.GetFullPath(dest));

            if (error is null)
            {
                Console.WriteLine($"Created symlink from {source} to {dest}");
                return true;
            }

            Console.WriteLine($"Unable to create symlink due to: \"{error}\" on {RuntimeInformation.OSDescription}, falling back to copy");

            File.Copy(source, dest);
            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dst"></param>
        /// <param name="recurse"></param>
        /// <param name="onFileCopied"></param>
        /// <returns>true if symlinked false if copied</returns>
        public static bool SymLinkOrCopyDirectory(string source, string dst, bool recurse = true,
            Action<string>? onFileCopied = null)
        {
            if (!Program.Config.UseSymlinks)
            {
                if (Directory.Exists(dst))
                    DeleteDirectory(dst);
                CopyDirectory(source, dst, recurse, onFileCopied);
                return false;
            }
            if (!linker.IsValid())
            {
                Console.Error.WriteLine($"Unable to use symlinks on {RuntimeInformation.OSDescription}, falling back to copy");
                CopyDirectory(source, dst, recurse, onFileCopied);
                return false;
            }

            if (Directory.Exists(dst))
                DeleteDirectory(dst);

            // Attempt to make symlinks to avoid unnecessary copy
            var error = linker.CreateLink(Path.GetFullPath(source), Path.GetFullPath(dst));

            if (error == null)
            {
                // Ensure the copied directory has permissions
                DirectoryPermissions(dst);
                Console.WriteLine($"Created symlink from {source} to {dst}");
                return true;
            }

            Console.WriteLine($"Unable to create symlink due to: \"{error}\" on {RuntimeInformation.OSDescription}, falling back to copy");
            CopyDirectory(source, dst, recurse, onFileCopied);
            return false;
        }

        public static void CopyDirectory(string source, string dst, bool recurse = true, Action<string>? onFileCopied = null)
        {
            DirectoryInfo dir = new(source);
            if (!Directory.Exists(dst))
                Directory.CreateDirectory(dst);

            foreach (var f in dir.GetFiles())
            {
                if (!f.Exists)
                    continue;
                var path = Path.Combine(dst, f.Name);
                f.CopyTo(path);
                onFileCopied?.Invoke(path);
            }

            if (recurse)
                foreach (var d in dir.GetDirectories())
                {
                    if (d.Exists && d.Attributes.HasFlag(FileAttributes.Directory))
                        CopyDirectory(d.FullName, Path.Combine(dst, d.Name), recurse);
                }
            // Ensure the copied directory has permissions
            DirectoryPermissions(dst);
        }

        public static string GetSubdir(string path)
        {
            var actualRoot = path;
            var dirs = Directory.GetDirectories(actualRoot);
            while (dirs.Length == 1 && Directory.GetFiles(actualRoot).Length == 0)
            {
                // If we have only one folder and no files, chances are we have to go one level deeper
                actualRoot = dirs[0];
                dirs = Directory.GetDirectories(actualRoot);
            }
            return actualRoot;
        }

        private static readonly string oldTempDir1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, Assembly.GetExecutingAssembly().GetName().Name + "_Temp");
        private static readonly string oldTempDir2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, Assembly.GetExecutingAssembly().GetName().Name + "_Tempv2");
        private static readonly string newTempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetExecutingAssembly().GetName().Name + "_Temp");

        public static void DeleteTempDir()
        {
            if (Directory.Exists(oldTempDir1))
                DeleteDirectory(oldTempDir1);

            if (Directory.Exists(oldTempDir2))
                DeleteDirectory(oldTempDir2);

            if (Directory.Exists(newTempDir))
                DeleteDirectory(newTempDir);

            if (Directory.Exists(Program.Config.CachePath))
                DeleteDirectory(Program.Config.CachePath);
        }

        // Conversion from PackageInfo to Id/version directory under cache directory.
        private static string GetCachedConfig(PackageInfo packageInfo) => Path.Combine(GetTempDir(), packageInfo.Id, packageInfo.Version.ToString());

        private const string libsFolder = "libs";
        private const string sourceLocation = "src";

        /// <summary>
        /// Returns the provided library path from the <see cref="PackageInfo"/> and the library name.
        /// </summary>
        /// <param name="packageInfo"></param>
        /// <returns></returns>
        public static string GetLibrary(PackageInfo packageInfo, string libName) => Path.Combine(GetCachedConfig(packageInfo), libsFolder, libName);

        /// <summary>
        /// Returns the path to the source location for the provided <see cref="PackageInfo"/>.
        /// </summary>
        /// <param name="packageInfo"></param>
        /// <returns></returns>
        public static string GetSource(PackageInfo packageInfo) => Path.Combine(GetCachedConfig(packageInfo), sourceLocation);

        // TODO: Make this configurable, QPM would have a config file that would be writable via `qpm cache set` or something similar
        private static string GetTempDir()
        {
            CreateDirectory(Program.Config.CachePath);
            DirectoryPermissions(Program.Config.CachePath);
            return Program.Config.CachePath;
        }

        public static string ReplaceFirst(this string str, string toFind, string toReplace)
        {
            var loc = str.IndexOf(toFind);
            if (loc < 0)
                return str;
            return str.Substring(0, loc) + toReplace + str.Substring(loc + toFind.Length);
        }

        public static string ReplaceLast(this string str, string toFind, string toReplace)
        {
            var loc = str.LastIndexOf(toFind);
            if (loc < 0)
                return str;
            return str.Substring(0, loc) + toReplace + str.Substring(loc + toFind.Length);
        }

        /// <summary>
        /// Returns the .so name of the provided PackageInfo, or null if it is headerOnly.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static string? GetSoName(this PackageInfo info, out bool overriden)
        {
            overriden = false;
            if (info.AdditionalData.TryGetValue(SupportedPropertiesCommand.HeadersOnly, out var elem) && elem.GetBoolean())
                return null;
            if (info.AdditionalData.TryGetValue(SupportedPropertiesCommand.OverrideSoName, out var name))
            {
                overriden = true;
                return name.GetString();
            }

            string ext = IsStaticLinking(info) ? ".a" : ".so";

            return "lib" + (info.Id + "_" + info.Version.ToString()).Replace('.', '_') + ext;
        }

        public static bool IsStaticLinking(this PackageInfo info) => info.AdditionalData.TryGetValue(SupportedPropertiesCommand.StaticLinking, out var elem) && elem.GetBoolean();
    }
}