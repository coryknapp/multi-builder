using System.Collections.Concurrent;
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

    public DateTime? LastBuildTime { get; set; }

    public string? GitBranch { get; set; }

    public DateTime? LastPullTime { get; internal set; }

    public ConcurrentQueue<LogLine> BuildLogs { get; } = new();

    public ConcurrentQueue<LogLine> RunLogs { get; } = new();

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
