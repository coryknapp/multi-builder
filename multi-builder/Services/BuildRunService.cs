using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BuildRunService
{
    private readonly BuildService BuildService;
    private readonly RunService RunService;

    public BuildRunService(BuildService buildService, RunService runService)
    {
        BuildService = buildService;
        RunService = runService;

        BuildService.BuildComplete += OnBuildComplete;
    }

    public void BuildAndRunProject(ManagedProject mp)
    {
        BuildService.EnqueueBuild(mp);
    }

    private void OnBuildComplete(object? sender, EventArgs e)
    {
        var buildEventArgs = e as BuildEventArgs;
        RunService.RunProject(buildEventArgs!.ManagedProject);
    }
}
