using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class RunService
{
    public OutputService OutputService { get; }

    public RunService(OutputService outputService)
    {
        OutputService = outputService;
    }

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
                OutputService.WriteHeaderLine($"Output for {managedProject.Name}");
                OutputService.WriteInfoLine(args.Data);
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
                OutputService.WriteHeaderLine($"--- Error output for {managedProject.Name} ---");
                OutputService.WriteErrorLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
