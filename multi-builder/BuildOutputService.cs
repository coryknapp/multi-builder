using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BuildOutputService
{
    private readonly BuildService BuildService;
    private readonly TextService TextService;

    public BuildOutputService(TextService textService, BuildService buildService)
    {
        BuildService = buildService;
        TextService = textService;
        BuildService.BuildStarted += OnBuildStarted;
        BuildService.BuildComplete += OnBuildComplete;
        BuildService.BuildQueueEmpty += OnBuildQueueEmpty;
        BuildService.BuildFailed += OnBuildFailed;
        BuildService.BuildRetried += OnBuildRetried;
        BuildService.OutputFileWritten += OnOutputFileWritten;
    }

    private void OnBuildStarted(object sender, EventArgs e)
    {
        TextService.WriteBuildingLine($"Building {GetManagedProjectName(e)}...");
    }

    private void OnBuildComplete(object sender, EventArgs e)
    {
        TextService.WriteSuccessLine($"Succesfully built {GetManagedProjectName(e)}.");
    }

    private void OnBuildQueueEmpty(object sender, EventArgs e)
    {
        TextService.WriteSuccessLine("All builds complete.");
    }

    private void OnBuildFailed(object sender, EventArgs e)
    {
        TextService.WriteErrorLine($"Failed to build {GetManagedProjectName(e)}.");

        // TODO optional: display error details
    }

    private void OnBuildRetried(object sender, EventArgs e)
    {
        TextService.WriteBuildingLine($"Building {GetManagedProjectName(e)}...");
    }

    private void OnOutputFileWritten(object sender, EventArgs e)
    {
        TextService.WriteInfoLine($"Dumped build output to {GetManagedProjectName(e)}...");
    }

    private string GetManagedProjectName(EventArgs e) => (e as BuildEventArgs)?.ManagedProject?.Name;
}
