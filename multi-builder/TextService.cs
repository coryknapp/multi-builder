using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class TextService
{
    private bool promptDisplayed = false;
    private readonly object consoleLock = new object();

    public void WritePromptLine(string prompt)
    {
        lock (consoleLock)
        {
            Console.Write(prompt);
            promptDisplayed = true;
        }
    }

    public void WriteInfoLine(string message)
    {
        WriteLineWithPromptHandling(message, ConsoleColor.Gray);
    }

    public void WriteErrorLine(string message)
    {
        WriteLineWithPromptHandling(message, ConsoleColor.Red);
    }

    public void WriteSuccessLine(string message)
    {
        WriteLineWithPromptHandling(message, ConsoleColor.Green);
    }

    public void WriteBuildingLine(string message)
    {
        WriteLineWithPromptHandling(message, ConsoleColor.Cyan);
    }

    public void WriteHeaderLine(string message)
    {
        lock (consoleLock)
        {
            ClearCurrentLine();
            
            var originalBackground = Console.BackgroundColor;
            var originalForeground = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Cyan;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write($" --- {message} --- ");
            Console.BackgroundColor = originalBackground;
            Console.ForegroundColor = originalForeground;
            Console.WriteLine();
            
            RestorePrompt();
        }
    }

    private void WriteLineWithPromptHandling(string message, ConsoleColor color)
    {
        lock (consoleLock)
        {
            ClearCurrentLine();
            
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
            
            RestorePrompt();
        }
    }

    private void ClearCurrentLine()
    {
        if (promptDisplayed)
        {
            // Move cursor to beginning of line and clear it
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            promptDisplayed = false;
        }
    }

    private void RestorePrompt()
    {
        Console.Write(">");
        promptDisplayed = true;
    }
}
