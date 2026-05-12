using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class LogCollectorService
{
    public void AddBuildLog(ManagedProject project, string content, LogSource source)
    {
        if (string.IsNullOrEmpty(content))
            return;

        var logLine = new LogLine(content, source);
        project.BuildLogs.Enqueue(logLine);
    }

    public void AddRunLog(ManagedProject project, string content, LogSource source)
    {
        if (string.IsNullOrEmpty(content))
            return;

        var logLine = new LogLine(content, source);
        project.RunLogs.Enqueue(logLine);
    }

    public void ClearBuildLogs(ManagedProject project)
    {
        while (project.BuildLogs.TryDequeue(out _)) { }
    }

    public void ClearRunLogs(ManagedProject project)
    {
        while (project.RunLogs.TryDequeue(out _)) { }
    }

    public IReadOnlyList<LogLine> GetBuildLogs(ManagedProject project)
    {
        return project.BuildLogs.ToArray();
    }

    public IReadOnlyList<LogLine> GetRunLogs(ManagedProject project)
    {
        return project.RunLogs.ToArray();
    }

    public IReadOnlyList<string> GetBuildLogContent(ManagedProject project)
    {
        return project.BuildLogs.Select(l => l.Content).ToArray();
    }

    public IReadOnlyList<string> GetRunLogContent(ManagedProject project)
    {
        return project.RunLogs.Select(l => l.Content).ToArray();
    }
}