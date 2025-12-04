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
            WriteBuildOutputLine(line);
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
            WriteBuildOutputLine(line);
        }
        Console.WriteLine("----- End of Build Output. Press Enter to return. -----");
        _ = Console.ReadLine();
        Console.Clear();
    }

    private void WriteBuildOutputLine(string line) => Console.WriteLine(line);

    private void WriteRunOutputLine(string line) => Console.WriteLine(line);
}
