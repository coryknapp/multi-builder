using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class OutputService
{
    public OutputService()
    {
    }


    public void PrintBuildOutput(ManagedProject managedProject)
    {
        Console.Clear();
        foreach (var line in managedProject.BuildOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
        {
            this.WriteBuildOutputLine(line);
        }
        Console.WriteLine("----- End of Build Output. Press Enter to return. -----");
        _ = Console.ReadLine();
        Console.Clear();
    }

    public void PrintRunOutput(ManagedProject managedProject)
    {
        Console.Clear();
        if(managedProject.LiveOutput == null)
        {
            Console.WriteLine("No live output available.");
            Console.WriteLine("----- Press Enter to return. -----");
            _ = Console.ReadLine();
            return;
        }
        foreach (var line in managedProject.LiveOutput)
        {
            this.WriteRunOutputLine(line);
        }
        Console.WriteLine("----- End of Build Output. Press Enter to return. -----");
        _ = Console.ReadLine();
        Console.Clear();
    }

    public void PrintBuildErrors(ManagedProject managedProject)
    {
        Console.Clear();
        Console.WriteLine($"===== Build Errors for {managedProject.Name} =====");
        Console.WriteLine();

        if (managedProject.ErrorMessages == null || !managedProject.ErrorMessages.Any())
        {
            Console.WriteLine("No error messages available.");
            Console.WriteLine("----- Press Enter to return. -----");
            _ = Console.ReadLine();
            Console.Clear();
            return;
        }

        // Filter to only include errors, not warnings
        var errorsOnly = managedProject.ErrorMessages
            .Where(msg => msg.Contains(": error ", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (errorsOnly.Count == 0)
        {
            Console.WriteLine("No errors found (warnings filtered out).");
            Console.WriteLine("----- Press Enter to return. -----");
            _ = Console.ReadLine();
            Console.Clear();
            return;
        }

        int errorCount = 0;
        foreach (var error in errorsOnly)
        {
            errorCount++;
            WriteErrorLine(error);
        }

        Console.WriteLine();
        Console.WriteLine($"----- {errorCount} error(s). Press Enter to return. -----");
        _ = Console.ReadLine();
        Console.Clear();
    }

    private void WriteBuildOutputLine(string line) => Console.WriteLine(line);

    private void WriteRunOutputLine(string line) => Console.WriteLine(line);

    private void WriteErrorLine(string line) => Console.WriteLine(line);
}
