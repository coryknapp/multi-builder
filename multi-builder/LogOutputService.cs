using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;

public class LogOutputService
{
    private static readonly Color EvenRowBackground = Color.Black;
    private static readonly Color OddRowBackground = new(40, 40, 40);

    private readonly LogCollectorService logCollectorService;

    public LogOutputService(LogCollectorService logCollectorService)
    {
        this.logCollectorService = logCollectorService;
    }

    public void PrintBuildOutput(ManagedProject managedProject)
    {
        var allLogs = logCollectorService.GetBuildLogs(managedProject);
        var filteredLogs = FilterLogsByTimeRange(allLogs, GetStartTime(), GetEndTime());

        Console.Clear();
        PrintLogHeader("Build Output", filteredLogs, allLogs.Count);
        PrintLogLines(filteredLogs);
        PrintFooterAndWait();
    }

    public void PrintRunOutput(ManagedProject managedProject)
    {
        var allLogs = logCollectorService.GetRunLogs(managedProject);
        var filteredLogs = FilterLogsByTimeRange(allLogs, GetStartTime(), GetEndTime());

        Console.Clear();
        PrintLogHeader("Run Output", filteredLogs, allLogs.Count);
        PrintLogLines(filteredLogs);
        PrintFooterAndWait();
    }

    public void PrintBuildErrors(ManagedProject managedProject)
    {
        Console.WriteLine("Not Implemented yet.");
    }

    private void PrintLogHeader(string description, IReadOnlyList<LogLine> displayedLogs, int totalCount)
    {
        var rule = new Rule($"[bold cyan]{description}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("cyan")
        };
        AnsiConsole.Write(rule);

        var startTime = GetStartTime();
        var endTime = GetEndTime();
        var timeRangeMinutes = RecentMillisecondCount() / 60000;

        AnsiConsole.MarkupLine($"[dim]Time range: {startTime:HH:mm:ss} - {endTime:HH:mm:ss} (last {timeRangeMinutes} minutes)[/]");

        if (displayedLogs.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No logs in time range[/] [dim](0 of {totalCount} total)[/]");
        }
        else if (displayedLogs.Count == totalCount)
        {
            AnsiConsole.MarkupLine($"[green]Showing all {displayedLogs.Count} logs[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Showing {displayedLogs.Count} of {totalCount} logs[/]");
        }

        if (displayedLogs.Count > 0)
        {
            var firstLog = displayedLogs.Min(l => l.Timestamp);
            var lastLog = displayedLogs.Max(l => l.Timestamp);
            AnsiConsole.MarkupLine($"[dim]Log timestamps: {firstLog:HH:mm:ss.fff} - {lastLog:HH:mm:ss.fff}[/]");
        }

        Console.WriteLine();
    }

    private void PrintFooterAndWait()
    {
        Console.WriteLine();
        AnsiConsole.MarkupLine("[dim]----- Press Enter to return. -----[/]");
        _ = Console.ReadLine();
        Console.Clear();
    }

    // We'll figure out a way for the user to specify how much of the log they want to see, but for now we'll just show the last 10 minutes of logs.
    private int RecentMillisecondCount() =>
        10 * 60 * 1000; // Last 10 minutes

    private DateTime GetStartTime() => DateTime.Now.AddMilliseconds(-RecentMillisecondCount());

    private DateTime GetEndTime() => DateTime.Now;

    private IReadOnlyList<LogLine> FilterLogsByTimeRange(IEnumerable<LogLine> logLines, DateTime startTime, DateTime endTime)
    {
        if (endTime < startTime)
        {
            throw new ArgumentException("End time must be greater than or equal to start time.");
        }

        return logLines
            .Where(line => line.Timestamp >= startTime && line.Timestamp <= endTime)
            .OrderBy(line => line.Timestamp)
            .ToList();
    }

    private void PrintLogLines(IReadOnlyList<LogLine> logLines)
    {
        if (logLines.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No log lines to display.[/]");
            return;
        }

        for (int i = 0; i < logLines.Count; i++)
        {
            WriteAlternatingLine(logLines[i], i);
        }
    }

    private void WriteAlternatingLine(LogLine logLine, int lineIndex)
    {
        var background = lineIndex % 2 == 0 ? EvenRowBackground : OddRowBackground;
        var foreground = logLine.Source is LogSource.BuildStdErr or LogSource.RunStdErr 
            ? Color.Red 
            : Color.White;

        var content = logLine.Content;

        if (this.PrintTimeStamp())
        {
            var timestamp = $"[{logLine.Timestamp:HH:mm:ss.fff}] ";
            content = timestamp + content;
        }

        var maxWidth = Console.WindowWidth - 1;
        var style = new Style(foreground, background);

        // Write line in chunks if it exceeds max width
        while (content.Length > 0)
        {
            var chunk = content.Length > maxWidth 
                ? content[..maxWidth] 
                : content;
            
            var padding = Math.Max(0, maxWidth - chunk.Length);
            var paddedChunk = chunk + new string(' ', padding) + "\n";
            
            AnsiConsole.Write(new Text(paddedChunk, style));
            
            content = content.Length > maxWidth 
                ? content[maxWidth..] 
                : string.Empty;
        }
    }

    private bool PrintTimeStamp()
    {
        return false;
    }
}
