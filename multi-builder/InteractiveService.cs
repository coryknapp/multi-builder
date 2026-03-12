using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

public class InteractiveService : IFullScreenView
{
    private readonly OptionService optionService;
    private readonly BuildService buildService;
    private readonly RunService runService;
    private readonly BuildRunService buildRunService;
    private readonly LogOutputService logOutputService;
    private readonly KillService killService;
    private readonly GitService gitService;
    private readonly FullScreenViewService fullScreenViewService;
    private readonly ErrorViewerService errorViewerService;

    private IList<ManagedProject> managedProjects = [];
    private int selectedIndex = 0;
    private LiveDisplayContext? liveContext;
    private int animationFrame = 0;

    // Animation frames for different states
    private readonly string[] dotsAnimation = { "⠀", "⠁", "⠃", "⠇", "⠏", "⠟", "⠿", "⡿", "⣿", "⣾", "⣼", "⣸", "⢸", "⠸", "⠘", "⠈" };
    private readonly string[] buildFrames = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂" };

    private DateTime cursorHideTime;

    public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(200);

    public InteractiveService(
        BuildService buildService,
        RunService runService,
        BuildRunService buildRunService,
        LogOutputService outputService,
        KillService killService,
        OptionService optionService,
        GitService gitService,
        FullScreenViewService fullScreenViewService,
        ErrorViewerService errorViewerService)
    {
        this.buildService = buildService;
        this.runService = runService;
        this.buildRunService = buildRunService;
        this.logOutputService = outputService;
        this.killService = killService;
        this.optionService = optionService;
        this.gitService = gitService;
        this.fullScreenViewService = fullScreenViewService;
        this.errorViewerService = errorViewerService;
    }

    public async Task StartInteractiveMode(IList<ManagedProject> projects, CancellationToken cancellationToken = default)
    {
        this.managedProjects = projects;
        this.selectedIndex = 0;

        await AnsiConsole.Live(CreateInteractiveTable())
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                liveContext = ctx;
                await fullScreenViewService.ShowViewAsync(this, cancellationToken);
            });
    }

    public void OnActivated()
    {
        UpdateCursorHideTime();
    }

    public void OnDeactivated()
    {
        // Cleanup if needed
    }

    public void Render()
    {
        animationFrame++;
        liveContext?.UpdateTarget(CreateInteractiveTable());
    }

    public bool HandleKey(ConsoleKeyInfo keyInfo)
    {
        bool hasShift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
        bool hasAlt = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0;

        ExecutionMode mode = GetExecutionMode(hasShift, hasAlt);

        // If the cursor is hidden, show it, but suppress the key action
        if (!ShowCursor() && mode == ExecutionMode.SelectedOnly)
        {
            UpdateCursorHideTime();
            return true; // Continue running
        }

        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                selectedIndex = Math.Max(0, selectedIndex - 1);
                Render();
                break;

            case ConsoleKey.DownArrow:
                selectedIndex = Math.Min(managedProjects.Count - 1, selectedIndex + 1);
                Render();
                break;

            case ConsoleKey.B: // Build
                ExecuteForProjects(mode, mp => buildService.EnqueueBuild(mp));
                break;

            case ConsoleKey.R: // Run
                ExecuteForProjects(mode, mp => runService.RunProject(mp));
                break;

            case ConsoleKey.P: // Build then Run
                ExecuteForProjects(mode, mp => _ = Task.Run(() => buildRunService.BuildAndRunProject(mp)));
                break;

            case ConsoleKey.E: // Show Errors
                ExecuteForProjects(mode, mp => ShowBuildErrors(mp));
                break;

            case ConsoleKey.O: // Show Output
                ExecuteForProjects(mode, mp => ShowProjectOutput(mp));
                break;

            case ConsoleKey.L: // Show Last build output
                ExecuteForProjects(mode, mp => ShowBuildOutput(mp));
                break;

            case ConsoleKey.K: // Kill/Stop
                ExecuteForProjects(mode, mp => StopProject(mp), delay: 0);
                break;

            case ConsoleKey.Q: // Quit
            case ConsoleKey.Escape:
                return false; // Stop running
        }

        UpdateCursorHideTime();
        return true; // Continue running
    }

    public void Stop() => fullScreenViewService.Stop();

    private enum ExecutionMode
    {
        SelectedOnly,
        AllProjects,
        AllExceptSelected
    }

    private ExecutionMode GetExecutionMode(bool hasShift, bool hasAlt)
    {
        if (hasShift && hasAlt)
            return ExecutionMode.AllExceptSelected;
        else if (hasShift)
            return ExecutionMode.AllProjects;
        else
            return ExecutionMode.SelectedOnly;
    }

    private void ExecuteForProjects(ExecutionMode mode, Action<ManagedProject> action, int delay = 300)
    {
        switch (mode)
        {
            case ExecutionMode.SelectedOnly:
                if (selectedIndex >= 0 && selectedIndex < managedProjects.Count)
                {
                    action(managedProjects[selectedIndex]);
                }
                break;

            case ExecutionMode.AllProjects:
                foreach (var mp in managedProjects)
                {
                    action(mp);
                    if (delay > 0) Task.Delay(delay).Wait();
                }
                break;

            case ExecutionMode.AllExceptSelected:
                for (int i = 0; i < managedProjects.Count; i++)
                {
                    if (i != selectedIndex)
                    {
                        action(managedProjects[i]);
                        if (delay > 0) Task.Delay(delay).Wait();
                    }
                }
                break;
        }
    }

    private Table CreateInteractiveTable()
    {
        var table = new Table();

        table.AddColumn(new TableColumn("[bold]#[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Project[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Errors[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Last Build[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Git Branch[/]").Centered());

        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Grey);

        for (int i = 0; i < managedProjects.Count; i++)
        {
            var mp = managedProjects[i];
            var isSelected = ShowCursor() && (i == selectedIndex);

            var rowStyle = isSelected ? "[on blue]" : "";
            var endStyle = isSelected ? "[/]" : "";

            table.AddRow(
                $"{rowStyle}{i + 1}{endStyle}",
                $"{rowStyle}{GetProjectName(mp)}{endStyle}",
                $"{rowStyle}{GetAnimatedStatusMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetErrorCountMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetLastBuildMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetGitBranchMarkup(mp)}{endStyle}"
            );
        }

        table.Caption("[dim]↑↓: Select | [bold]B[/]: Build | [bold]R[/]: Run | [bold]P[/]: Build+Run | [bold]E[/]: Errors | [bold]O[/]: Output | [bold]L[/]: Build Log | [bold]K[/]: Kill |  [bold]Shift-(Key)[/]: Perform action on all | [bold]Alt-Shift-(Key)[/]: Perform action on all, other then selected. | [bold]Q[/]: Quit[/]");

        return table;
    }

    private string GetProjectName(ManagedProject mp) => mp.Name;

    private string GetAnimatedStatusMarkup(ManagedProject mp)
    {
        // stager in the frame for visual variety
        int frameIndex = animationFrame + (mp.GetHashCode() % 10);

        if (mp.IsBuilding)
        {
            var spinner = buildFrames[frameIndex % buildFrames.Length];
            return $"[yellow]{spinner} Building[/]";
        }
        else if (mp.IsRunning)
        {
            var dots = dotsAnimation[frameIndex % dotsAnimation.Length];
            return $"[green]{dots} Running[/]";
        }
        else if (buildService.IsProjectEnqueued(mp))
        {
            var dots = dotsAnimation[frameIndex % dotsAnimation.Length];
            return mp.BuildFailure ? $"[red]{dots} Enqueued[/]" : $"[cyan]{dots} Enqueued[/]";
        }
        if (mp.BuildFailure) return "[red]❌ Failed[/]";
        if (mp.LastBuildTime.HasValue) return "[green]✅ Ready[/]";
        return "[dim]Not Built[/]";
    }

    private string GetErrorCountMarkup(ManagedProject mp)
    {
        var errorCount = mp.ErrorMessages?.Count() ?? 0;

        if (errorCount == 0)
        {
            return mp.BuildFailure ? "[dim]0[/]" : "[dim]-[/]";
        }

        // Color coding based on error count
        if (errorCount >= 10)
            return $"[red bold]{errorCount}[/]";
        return $"[red]{errorCount}[/]";
    }


    private string GetLastBuildMarkup(ManagedProject mp)
    {
        if (mp.BuildFailure) return "[red]Failed[/]";
        if (mp.LastBuildTime.HasValue)
        {
            var timeSpan = DateTime.Now - mp.LastBuildTime.Value;
            if (timeSpan.TotalMinutes < 1) return "[green]Just now[/]";
            if (timeSpan.TotalHours < 1) return $"[yellow]{(int)timeSpan.TotalMinutes}m ago[/]";
            return $"[orange1]{(int)timeSpan.TotalHours}h ago[/]";
        }
        return "[dim]Never[/]";
    }

    private string GetGitBranchMarkup(ManagedProject mp)
    {
        if (mp.GitBranch == null)
        {
            return "[dim]-[/]";
        }

        var pullTimeMarkup = this.GetLastPullTimeMarkup(mp.LastPullTime);
        return $"[cyan]{mp.GitBranch}[/] {pullTimeMarkup}";
    }

    private string GetLastPullTimeMarkup(DateTime? lastPullTime)
    {
        if (!lastPullTime.HasValue)
        {
            return "[dim](no pull)[/]";
        }

        var timeSpan = DateTime.Now - lastPullTime.Value;

        if (timeSpan.TotalMinutes < 1)
            return "[green](just now)[/]";
        if (timeSpan.TotalMinutes < 60)
            return $"[green]({(int)timeSpan.TotalMinutes}m ago)[/]";
        if (timeSpan.TotalHours < 24)
            return $"[yellow]({(int)timeSpan.TotalHours}h ago)[/]";
        if (timeSpan.TotalDays < 7)
            return $"[orange1]({(int)timeSpan.TotalDays}d ago)[/]";

        return $"[red]({(int)timeSpan.TotalDays}d ago)[/]";
    }

    private void ShowBuildErrors(ManagedProject project)
    {
        liveContext?.UpdateTarget(new Text(""));
        errorViewerService.ShowErrors(project);
        fullScreenViewService.Pause();
        Console.Clear();
        
        // Run error viewer as modal view
        Task.Run(async () => 
        {
            await fullScreenViewService.ShowViewAsync(errorViewerService);
        }).Wait();
        
        fullScreenViewService.Resume();
    }

    private void ShowProjectOutput(ManagedProject project)
    {
        ShowOutput(() => logOutputService.PrintRunOutput(project));
    }

    private void ShowBuildOutput(ManagedProject project)
    {
        ShowOutput(() => logOutputService.PrintBuildOutput(project));
    }

    private void ShowOutput( Action printAction)
    {
        liveContext?.UpdateTarget(new Text(""));
        fullScreenViewService.Pause();
        Task.Delay(200).Wait();
        printAction.Invoke();
        fullScreenViewService.Resume();
    }

    private void StopProject(ManagedProject managedProject) => killService.KillProject(managedProject);

    private void UpdateCursorHideTime()
    {
        cursorHideTime = DateTime.Now.AddSeconds(optionService.HideCursorSeconds);
    }

    private bool ShowCursor()
    {
        if (optionService.HideCursorSeconds == 0)
        {
            return true;
        }
        return DateTime.Now < cursorHideTime;
    }
}