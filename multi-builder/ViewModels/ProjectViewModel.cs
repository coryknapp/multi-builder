using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

public class ProjectViewModel : INotifyPropertyChanged
{
    private readonly BuildService buildService;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public ManagedProject Project { get; }

    public string Name => Project.Name;

    public string Status
    {
        get
        {
            if (Project.IsBuilding) return "🔨 Building";
            if (Project.IsRunning) return "▶️ Running";
            if (buildService.IsProjectEnqueued(Project)) return "⏳ Queued";
            if (Project.BuildFailure) return "❌ Failed";
            if (Project.LastBuildTime.HasValue) return "✅ Ready";
            return "⚪ Not Built";
        }
    }

    public IBrush StatusColor
    {
        get
        {
            if (Project.IsBuilding) return Brushes.Orange;
            if (Project.IsRunning) return Brushes.LimeGreen;
            if (buildService.IsProjectEnqueued(Project)) return Brushes.Cyan;
            if (Project.BuildFailure) return Brushes.Red;
            if (Project.LastBuildTime.HasValue) return Brushes.Green;
            return Brushes.Gray;
        }
    }

    public int ErrorCount => Project.ErrorMessages?.Count() ?? 0;

    public string LastBuild
    {
        get
        {
            if (Project.BuildFailure) return "Failed";
            if (!Project.LastBuildTime.HasValue) return "Never";

            var span = DateTime.Now - Project.LastBuildTime.Value;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
            return $"{(int)span.TotalHours}h ago";
        }
    }

    public string GitBranch => Project.GitBranch ?? "-";

    public string LastPull
    {
        get
        {
            if (!Project.LastPullTime.HasValue) return "";

            var span = DateTime.Now - Project.LastPullTime.Value;
            if (span.TotalMinutes < 1) return "(just now)";
            if (span.TotalHours < 1) return $"({(int)span.TotalMinutes}m ago)";
            if (span.TotalHours < 24) return $"({(int)span.TotalHours}h ago)";
            return $"({(int)span.TotalDays}d ago)";
        }
    }

    public ProjectViewModel(ManagedProject project, BuildService buildService)
    {
        Project = project;
        this.buildService = buildService;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(LastBuild));
        OnPropertyChanged(nameof(GitBranch));
        OnPropertyChanged(nameof(LastPull));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}