using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class OutputService
{
    private TextService TextService { get; }

    public OutputService(TextService textService)
    {
        TextService = textService;
    }

    public void PrintStatus(IEnumerable<ManagedProject> managedProjects)
    {
        this.TextService.WriteHeaderLine("Current running processes:");
        int index = 1;
        foreach (var mp in managedProjects)
        {
            if (mp.IsBuilding)
            {
                TextService.WriteBuildingLine($"{index++}: {mp.Name} (Building)");
            }
            else if (mp.IsRunning)
            {
                TextService.WriteSuccessLine($"{index++}: {mp.Name} (Running)");
            }
            else if (mp.BuildFailure)
            {
                TextService.WriteErrorLine($"{index++}: {mp.Name} (Failed)");
            }
            else if (mp.LastBuildTime == null)
            {
                TextService.WriteInfoLine($"{index++}: {mp.Name} (never built)");
            }
            else
            {
                TextService.WriteInfoLine($"{index++}: {mp.Name} (built success at ${mp.LastBuildTime})");
            }
        }
    }

    public void PrintHelpMessage(List<Command> commands)
    {
        foreach (var cmd in commands)
        {
            TextService.WriteInfoLine($"{string.Join(", ", cmd.Invocations)}: {cmd.HelpString}");
        }
    }

    public void PrintBuildOutput(ManagedProject managedProject)
    {
        TextService.WriteHeaderLine($"Last build output for {managedProject.Name}");
        if (!string.IsNullOrEmpty(managedProject.LastBuildOutput))
        {
            TextService.WriteInfoLine(managedProject.LastBuildOutput);
        }
        else
        {
            TextService.WriteInfoLine("No build output available.");
        }
    }

    public void PrintRunOutput(ManagedProject managedProject)
    {
        if (managedProject.LiveOutput.Count > 0)
        {
            TextService.WriteHeaderLine($"Run output for {managedProject.Name}");
            foreach (var line in managedProject.LiveOutput)
            {
                TextService.WriteInfoLine(line);
            }
        }
        else
        {
            TextService.WriteInfoLine("No output available yet.");
        }
    }

    public void EnableLiveRunOutput(ManagedProject managedProject)
    {
        managedProject.PrintOutputInRealTime = true;
    }

    public void DisableLiveRunOutput(ManagedProject managedProject)
    {
        managedProject.PrintOutputInRealTime = false;
    }
}
