using System.Collections.Generic;
using System.Diagnostics;

public class ManagedProject
{
    public string Name { get; set; }

    public string WorkingDirectory { get; set; }

    public Process BuildProcess { get; set; }

    public Process RunProcess { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsBuilding => BuildProcess != null && !BuildProcess.HasExited;

    public bool IsRunning => RunProcess != null && !RunProcess.HasExited;

    public bool BuildFailure { get; set; }

    public IEnumerable<string> ErrorMessages { get; set; }

    public int RetryAttempts { get; set; } = 0;

    public bool RetryEligible { get; set; }

    public string LastBuildOutput { get; set; }

    public DateTime? LastBuildTime { get; set; }

    public List<string> LiveOutput { get; set; } = new List<string>();

    public bool PrintOutputInRealTime { get; internal set; }
}
