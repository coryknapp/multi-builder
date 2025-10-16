using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class OutputService
{
    public void WriteInfoLine(string message)
    {
        lock (this)
        {
            Console.WriteLine(message);
        }
    }

    public void WriteErrorLine(string message)
    {
        lock (this)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }

    public void WriteSuccessLine(string message)
    {
        lock (this)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }

    public void WriteBuildingLine(string message)
    {
        lock (this)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }

    public void WriteHeaderLine(string message)
    {
        lock (this)
        {
            var originalBackground = Console.BackgroundColor;
            var originalForeground = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Cyan;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write($" --- {message} ---");
            Console.BackgroundColor = originalBackground;
            Console.ForegroundColor = originalForeground;
            Console.WriteLine();
        }
    }
}
