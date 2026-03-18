using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly BuildService buildService;
    private readonly RunService runService;
    private readonly BuildRunService buildRunService;
    private readonly KillService killService;
    private readonly DispatcherTimer refreshTimer;
    private readonly LogCollectorService logCollectorService;

    private ManagedProject? selectedProject;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectViewModel> Projects { get; } = new();

    public ProjectViewModel? SelectedProject
    {
        get => Projects.FirstOrDefault(p => p.Project == selectedProject);
        set
        {
            selectedProject = value?.Project;
            OnPropertyChanged();
        }
    }

    public MainViewModel(
        IList<ManagedProject> managedProjects,
        BuildService buildService,
        RunService runService,
        BuildRunService buildRunService,
        KillService killService,
        LogCollectorService logCollectorService)
    {
        this.buildService = buildService;
        this.runService = runService;
        this.buildRunService = buildRunService;
        this.killService = killService;
        this.logCollectorService = logCollectorService;

        foreach (var project in managedProjects)
        {
            Projects.Add(new ProjectViewModel(project, buildService));
        }

        if (Projects.Count > 0)
        {
            selectedProject = Projects[0].Project;
        }

        // Use DispatcherTimer which runs on UI thread
        refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        refreshTimer.Tick += (_, _) => RefreshAll();
        refreshTimer.Start();
    }

    public void BuildSelected()
    {
        if (selectedProject != null)
            buildService.EnqueueBuild(selectedProject);
    }

    public void BuildAll()
    {
        foreach (var p in Projects)
            buildService.EnqueueBuild(p.Project);
    }

    public void RunSelected()
    {
        if (selectedProject != null)
            runService.RunProject(selectedProject);
    }

    public void RunAll()
    {
        foreach (var p in Projects)
            runService.RunProject(p.Project);
    }

    public void BuildAndRunSelected()
    {
        if (selectedProject != null)
            buildRunService.BuildAndRunProject(selectedProject);
    }

    public void KillSelected()
    {
        if (selectedProject != null)
            killService.KillProject(selectedProject);
    }

    public void KillAll()
    {
        foreach (var p in Projects)
            killService.KillProject(p.Project);
    }

    private void RefreshAll()
    {
        foreach (var p in Projects)
            p.Refresh();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void ShowBuildLogs()
    {
        if (selectedProject != null)
            LogViewerWindow.Show(selectedProject, logCollectorService, LogViewType.Build);
    }

    public void ShowRunLogs()
    {
        if (selectedProject != null)
            LogViewerWindow.Show(selectedProject, logCollectorService, LogViewType.Run);
    }
    public void ShowBuildLogs(ProjectViewModel project)
    {
        LogViewerWindow.Show(project.Project, logCollectorService, LogViewType.Build);
    }

    public void ShowRunLogs(ProjectViewModel project)
    {
        LogViewerWindow.Show(project.Project, logCollectorService, LogViewType.Run);
    }

    public void ShowBuildLogsSelected()
    {
        if (selectedProject != null)
            LogViewerWindow.Show(selectedProject, logCollectorService, LogViewType.Build);
    }

    public void ShowRunLogsSelected()
    {
        if (selectedProject != null)
            LogViewerWindow.Show(selectedProject, logCollectorService, LogViewType.Run);
    }
}