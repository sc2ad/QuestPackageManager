using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    }
}