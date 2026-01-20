using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

public class InteractiveService
{
    private readonly OptionService optionService;
    private readonly BuildService buildService;
    private readonly RunService runService;
    private readonly BuildRunService buildRunService;
    private readonly OutputService outputService;
    private readonly KillService killService;
    private readonly GitService gitService;

    private int selectedIndex = 0;
    private bool isRunning = false;
    private bool pauseDisplayUpdates = false;
    private LiveDisplayContext liveContext;
    private int animationFrame = 0; // Add animation frame counter

    // Animation frames for different states
    private readonly string[] dotsAnimation = { "⠀", "⠁", "⠃", "⠇", "⠏", "⠟", "⠿", "⡿", "⣿", "⣾", "⣼", "⣸", "⢸", "⠸", "⠘", "⠈" };
    private readonly string[] buildFrames = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂" };

    private DateTime cursorHideTime;

    public InteractiveService(
        BuildService buildService,
        RunService runService,
        BuildRunService buildRunService,
        OutputService outputService,
        KillService killService,
        OptionService optionService,
        GitService gitService)
    {
        this.buildService = buildService;
        this.runService = runService;
        this.buildRunService = buildRunService;
        this.outputService = outputService;
        this.killService = killService;
        this.optionService = optionService;
        this.gitService = gitService;
    }

    public async Task StartInteractiveMode(IList<ManagedProject> managedProjects, CancellationToken cancellationToken = default)
    {
        isRunning = true;
        pauseDisplayUpdates = false;

        selectedIndex = 0;

        await AnsiConsole.Live(CreateInteractiveTable(managedProjects))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                liveContext = ctx;

                var inputTask = Task.Run(() => HandleHotkeyInput(managedProjects, cancellationToken));
                var updateTask = Task.Run(async () =>
                {
                    while (isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (!pauseDisplayUpdates)
                            {
                                animationFrame++; // Increment animation frame
                                UpdateDisplay(managedProjects);
                            }
                            await Task.Delay(200, cancellationToken); // Faster updates for animation (200ms instead of 1000ms)
                        }catch(Exception)
                        {
                            // for breakpoints
                            return;
                        }
                    }
                });

                await Task.WhenAny(inputTask, updateTask);
                isRunning = false;
            });
    }

    private void HandleHotkeyInput(IList<ManagedProject> managedProjects, CancellationToken cancellationToken)
    {
        this.UpdateCursorHideTime();
        while (isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var key = Console.ReadKey(true);
                bool hasShift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
                bool hasAlt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
                
                ExecutionMode mode = GetExecutionMode(hasShift, hasAlt);

                // if the cursor is hidden, show it, but suppress the key action
                if (!ShowCursor() && mode == ExecutionMode.SelectedOnly)
                {
                    UpdateCursorHideTime();
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = Math.Max(0, selectedIndex - 1);
                        UpdateDisplay(managedProjects);
                        break;

                    case ConsoleKey.DownArrow:
                        selectedIndex = Math.Min(managedProjects.Count - 1, selectedIndex + 1);
                        UpdateDisplay(managedProjects);
                        break;

                    case ConsoleKey.B: // Build
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            buildService.EnqueueBuild(mp);
                        });
                        break;

                    case ConsoleKey.R: // Run
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            runService.RunProject(mp);
                        });
                        break;

                    case ConsoleKey.P: // Build then Run
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            _ = Task.Run(() => buildRunService.BuildAndRunProject(mp));
                        });
                        break;

                    case ConsoleKey.O: // Show Output
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            ShowProjectOutput(mp);
                        });
                        break;

                    case ConsoleKey.L: // Show Last build output
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            ShowBuildOutput(mp);
                        });
                        break;

                    case ConsoleKey.K: // Kill/Stop
                        ExecuteForProjects(mode, managedProjects, mp =>
                        {
                            StopProject(mp);
                        }, 0);
                        break;

                    case ConsoleKey.Q: // Quit
                    case ConsoleKey.Escape:
                        isRunning = false;
                        return;
                }

                this.UpdateCursorHideTime();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

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

    private void ExecuteForProjects(ExecutionMode mode, IList<ManagedProject> projects, Action<ManagedProject> action, int delay = 300)
    {
        switch (mode)
        {
            case ExecutionMode.SelectedOnly:
                // Apply to selected project only
                if (selectedIndex >= 0 && selectedIndex < projects.Count)
                {
                    action(projects[selectedIndex]);
                }
                break;

            case ExecutionMode.AllProjects:
                // Apply to all projects
                foreach (var mp in projects)
                {
                    action(mp);
                    // hack to prevent concurrency issues
                    Task.Delay(delay).Wait();
                }
                break;

            case ExecutionMode.AllExceptSelected:
                // Apply to all projects except the selected one
                for (int i = 0; i < projects.Count; i++)
                {
                    if (i != selectedIndex)
                    {
                        action(projects[i]);
                        // hack to prevent concurrency issues
                        Task.Delay(delay).Wait();
                    }
                }
                break;
        }
    }

    private void UpdateDisplay(IList<ManagedProject> managedProjects)
    {
        liveContext?.UpdateTarget(CreateInteractiveTable(managedProjects));
    }

    private Table CreateInteractiveTable(IList<ManagedProject> managedProjects)
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
        table.Title("[bold cyan]Multi-Builder Interactive Mode[/]");

        // Add rows with selection highlighting
        for (int i = 0; i < managedProjects.Count; i++)
        {
            var mp = managedProjects[i];
            var isSelected = this.ShowCursor() && (i == selectedIndex);

            var rowStyle = isSelected ? "[on blue]" : "";
            var endStyle = isSelected ? "[/]" : "";

            table.AddRow(
                $"{rowStyle}{i + 1}{endStyle}",
                $"{rowStyle}{GetProjectName(mp)}{endStyle}",
                $"{rowStyle}{GetAnimatedStatusMarkup(mp)}{endStyle}", // Use animated version
                $"{rowStyle}{GetErrorCountMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetLastBuildMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetGitBranchMarkup(mp)}{endStyle}"
            );
        }

        // Add hotkey instructions
        table.Caption("[dim]↑↓: Select | [bold]B[/]: Build | [bold]R[/]: Run | [bold]P[/]: Build+Run | [bold]O[/]: Output | [bold]L[/]: Build Log | [bold]K[/]: Kill |  [bold]Shift-(Key)[/]: Perform action on all | [bold]Alt-Shift-(Key)[/]: Perform action on all, other then selected. | [bold]Q[/]: Quit[/]");

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
        return $"[cyan]{mp.GitBranch}[/]";
    }

    private void ShowProjectOutput(ManagedProject project)
    {
        pauseDisplayUpdates = true;
        Task.Delay(200).Wait();
        outputService.PrintRunOutput(project);
        pauseDisplayUpdates = false;
    }

    private void ShowBuildOutput(ManagedProject project)
    {
        pauseDisplayUpdates = true;
        Task.Delay(200).Wait();
        outputService.PrintBuildOutput(project);
        pauseDisplayUpdates = false;
    }

    private void StopProject(ManagedProject managedProject) => killService.KillProject(managedProject);

    public void Stop()
    {
        isRunning = false;
    }

    private void UpdateCursorHideTime()
    {
        this.cursorHideTime = DateTime.Now.AddSeconds(this.optionService.HideCursorSeconds);
    }

    private bool ShowCursor()
    {
        if( this.optionService.HideCursorSeconds == 0)
        {
            return true;
        }
        return (DateTime.Now < cursorHideTime);
    }
}