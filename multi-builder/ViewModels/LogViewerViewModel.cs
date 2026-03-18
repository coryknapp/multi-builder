using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

public class LogViewerViewModel : INotifyPropertyChanged
{
    private readonly LogCollectorService logCollectorService;
    private readonly ManagedProject project;
    private readonly LogViewType logType;
    private readonly DispatcherTimer refreshTimer;

    private bool isPaused;
    private string searchText = string.Empty;
    private ObservableCollection<LogLineViewModel> filteredLines = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => $"{project.Name} - {(logType == LogViewType.Build ? "Build" : "Run")} Logs";

    public ObservableCollection<LogLineViewModel> FilteredLines
    {
        get => filteredLines;
        set { filteredLines = value; OnPropertyChanged(); }
    }

    public bool IsPaused
    {
        get => isPaused;
        set { isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(PauseButtonText)); }
    }

    public string PauseButtonText => IsPaused ? "▶ Resume" : "⏸ Pause";

    public string SearchText
    {
        get => searchText;
        set
        {
            searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public int TotalLineCount { get; private set; }
    public int FilteredLineCount => FilteredLines.Count;

    public LogViewerViewModel(ManagedProject project, LogCollectorService logCollectorService, LogViewType logType)
    {
        this.project = project;
        this.logCollectorService = logCollectorService;
        this.logType = logType;

        refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        refreshTimer.Tick += (_, _) => RefreshLogs();
        refreshTimer.Start();

        RefreshLogs();
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public void ScrollToEnd()
    {
        // Handled by the view
    }

    private void RefreshLogs()
    {
        if (IsPaused) return;

        var logs = logType == LogViewType.Build
            ? logCollectorService.GetBuildLogs(project)
            : logCollectorService.GetRunLogs(project);

        TotalLineCount = logs.Count;
        OnPropertyChanged(nameof(TotalLineCount));

        ApplyFilter(logs);
    }

    private void ApplyFilter(IReadOnlyList<LogLine>? logs = null)
    {
        logs ??= logType == LogViewType.Build
            ? logCollectorService.GetBuildLogs(project)
            : logCollectorService.GetRunLogs(project);

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? logs
            : logs.Where(l => l.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredLines.Clear();
        int index = 0;
        foreach (var log in filtered.OrderBy(l => l.Timestamp))
        {
            FilteredLines.Add(new LogLineViewModel(log, index++, SearchText));
        }

        OnPropertyChanged(nameof(FilteredLineCount));
    }

    public void Stop()
    {
        refreshTimer.Stop();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum LogViewType
{
    Build,
    Run
}

public class LogLineViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public LogLine LogLine { get; }
    public int Index { get; }
    public string SearchText { get; }

    public string Timestamp => LogLine.Timestamp.ToString("HH:mm:ss.fff");
    public string Content => LogLine.Content;
    public bool IsError => LogLine.Source is LogSource.BuildStdErr or LogSource.RunStdErr;
    public bool IsEvenRow => Index % 2 == 0;
    public bool IsMatch => !string.IsNullOrEmpty(SearchText) && 
                           Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    public LogLineViewModel(LogLine logLine, int index, string searchText)
    {
        LogLine = logLine;
        Index = index;
        SearchText = searchText;
    }
}