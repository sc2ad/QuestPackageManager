using QPM.Commands;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace QPM
{
    public static class Utils
    {
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
                    Arguments = "-c \"chmod +rw --recursive '" + absPath + "'\"",
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

        public static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
                DeleteDirectory(directory);

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        public static void CopyDirectory(string source, string dst, bool recurse = true, Action<string>? onFileCopied = null)
        {
            DirectoryInfo dir = new DirectoryInfo(source);
            if (!Directory.Exists(dst))
                Directory.CreateDirectory(dst);

            foreach (var f in dir.GetFiles())
            {
                var path = Path.Combine(dst, f.Name);
                f.CopyTo(path);
                onFileCopied?.Invoke(path);
            }

            if (recurse)
                foreach (var d in dir.GetDirectories())
                    CopyDirectory(d.FullName, Path.Combine(dst, d.Name), recurse);
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

        public static void DeleteTempDir()
        {
            var outter = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, Assembly.GetExecutingAssembly().GetName().Name + "_Temp");
            DeleteDirectory(outter);
        }

        public static string GetTempDir()
        {
            var outter = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, Assembly.GetExecutingAssembly().GetName().Name + "_Temp");
            CreateDirectory(outter);
            DirectoryPermissions(outter);
            return outter;
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
            return "lib" + (info.Id + "_" + info.Version.ToString()).Replace('.', '_') + ".so";
        }
    }
}