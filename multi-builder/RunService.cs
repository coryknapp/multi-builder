using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class RunService
{
    public void RunProject(ManagedProject managedProject)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {Program.RunCommand}",
            WorkingDirectory = managedProject.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        managedProject.RunProcess = process;

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }

            if (managedProject.PrintOutputInRealTime)
            {
                Program.WriteHeaderLine($"Output for {managedProject.Name}");
                Console.WriteLine(args.Data);
            }
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }

            if (managedProject.PrintOutputInRealTime)
            {
                Program.WriteHeaderLine($"--- Error output for {managedProject.Name} ---");
                Program.WriteErrorLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
