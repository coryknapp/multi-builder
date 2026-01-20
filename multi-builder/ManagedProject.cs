using System.Collections.Generic;
using System.Diagnostics;

public class ManagedProject
{
    public ManagedProject(string name, string workingDirectory)
    {
        Name = name;
        WorkingDirectory = workingDirectory;
    }

    public string Name { get; }

    public string WorkingDirectory { get; }

    public Process? BuildProcess { get; set; }

    public Process? RunProcess { get; set; }

    public bool IsBuilding => this.IsProcessRunning(this.BuildProcess);

    public bool IsRunning => this.IsProcessRunning(this.RunProcess);

    public bool BuildFailure { get; set; }

    public IEnumerable<string>? ErrorMessages { get; set; }

    public int RetryAttempts { get; set; } = 0;

    public string? BuildOutput { get; set; }

    public DateTime? LastBuildTime { get; set; }

    public List<string>? LiveOutput { get; set; }

    public string? GitBranch { get; set; }

    private bool IsProcessRunning(Process? process)
    {
        if (process == null) return false;

        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            // Process was never started or has been disposed
            return false;
        }
    }
}
