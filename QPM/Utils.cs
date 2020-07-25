using System;
using System.Collections.Generic;
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
    }
}