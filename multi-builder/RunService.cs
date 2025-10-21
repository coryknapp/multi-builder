using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class RunService
{
    private readonly TextService TextService;

    private readonly OptionService OptionService;

    public RunService(TextService textService, OptionService optionService)
    {
        TextService = textService;
        OptionService = optionService;
    }

    public void RunProject(ManagedProject managedProject)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {OptionService.RunCommand}",
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
                TextService.WriteHeaderLine($"Output for {managedProject.Name}");
                TextService.WriteInfoLine(args.Data);
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
                TextService.WriteHeaderLine($"--- Error output for {managedProject.Name} ---");
                TextService.WriteErrorLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
