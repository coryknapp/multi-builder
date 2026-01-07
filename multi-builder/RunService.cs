using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class RunService
{
    private readonly OptionService OptionService;
    private readonly KillService KillService;

    public RunService(OptionService optionService, KillService killService)
    {
        this.OptionService = optionService;
        this.KillService = killService;
    }

    public void RunProject(ManagedProject managedProject)
    {
        this.KillService.KillProject(managedProject);

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
        managedProject.LiveOutput = new List<string>();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
