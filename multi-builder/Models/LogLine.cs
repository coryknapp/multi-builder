using System;

public class LogLine
{
    public LogLine(string content, LogSource source)
    {
        Content = content;
        Timestamp = DateTime.Now;
        Source = source;
    }

    public string Content { get; }
    public DateTime Timestamp { get; }
    public LogSource Source { get; }
}

public enum LogSource
{
    BuildStdOut,
    BuildStdErr,
    RunStdOut,
    RunStdErr
}