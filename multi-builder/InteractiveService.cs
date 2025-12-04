using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

public class InteractiveService
{
    private readonly BuildService buildService;
    private readonly RunService runService;
    private readonly BuildRunService buildRunService;
    private readonly OutputService outputService;

    private int selectedIndex = 0;
    private bool isRunning = false;
    private bool pauseDisplayUpdates = false;
    private LiveDisplayContext liveContext;
    private int animationFrame = 0; // Add animation frame counter

    // Animation frames for different states
    private readonly string[] spinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private readonly string[] dotsAnimation = { "⠀", "⠁", "⠃", "⠇", "⠏", "⠟", "⠿", "⡿", "⣿", "⣾", "⣼", "⣸", "⢸", "⠸", "⠘", "⠈" };
    private readonly string[] buildFrames = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂" };


    public InteractiveService(
        BuildService buildService,
        RunService runService,
        BuildRunService buildRunService,
        OutputService outputService)
    {
        this.buildService = buildService;
        this.runService = runService;
        this.buildRunService = buildRunService;
        this.outputService = outputService;
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
                bool applyToAll = (key.Modifiers & ConsoleModifiers.Shift) != 0;

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

                    case ConsoleKey.B: // Build (Shift+B builds all)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            buildService.EnqueueBuild(mp);
                        });
                        break;

                    case ConsoleKey.R: // Run (Shift+R runs all)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            runService.RunProject(mp);
                        });
                        break;

                    case ConsoleKey.P: // Build and Run (Shift+P for all)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            _ = Task.Run(() => buildRunService.BuildAndRunProject(mp));
                        });
                        break;

                    case ConsoleKey.O: // Show Output (Shift+O shows output for all sequentially)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            ShowProjectOutput(mp);
                        });
                        break;

                    case ConsoleKey.L: // Show Last build output (Shift+L for all sequentially)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            ShowBuildOutput(mp);
                        });
                        break;

                    case ConsoleKey.K: // Kill/Stop (Shift+K stops all)
                        ExecuteForProjects(applyToAll, managedProjects, mp =>
                        {
                            StopProject(mp);
                        });
                        break;

                    case ConsoleKey.Q: // Quit
                    case ConsoleKey.Escape:
                        isRunning = false;
                        return;

                    case ConsoleKey.H: // Help
                        ShowHelpDialog();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ExecuteForProjects(bool all, IList<ManagedProject> projects, Action<ManagedProject> action)
    {
        if (!all)
        {
            // Apply to selected project only
            if (selectedIndex >= 0 && selectedIndex < projects.Count)
            {
                action(projects[selectedIndex]);
            }
            return;
        }

        // Apply to all projects
        foreach (var mp in projects)
        {
            action(mp);

            // hack to prevent concurrency issues
            Task.Delay(500).Wait();
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

        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Grey);
        table.Title("[bold cyan]Multi-Builder Interactive Mode[/]");

        // Add rows with selection highlighting
        for (int i = 0; i < managedProjects.Count; i++)
        {
            var mp = managedProjects[i];
            var isSelected = i == selectedIndex;

            var rowStyle = isSelected ? "[on blue]" : "";
            var endStyle = isSelected ? "[/]" : "";

            table.AddRow(
                $"{rowStyle}{i + 1}{endStyle}",
                $"{rowStyle}{GetProjectName(mp)}{endStyle}",
                $"{rowStyle}{GetAnimatedStatusMarkup(mp)}{endStyle}", // Use animated version
                $"{rowStyle}{GetErrorCountMarkup(mp)}{endStyle}",
                $"{rowStyle}{GetLastBuildMarkup(mp)}{endStyle}"
            );
        }

        // Add hotkey instructions
        table.Caption("[dim]↑↓: Select | [bold]B[/]: Build | [bold]R[/]: Run | [bold]P[/]: Build+Run | [bold]O[/]: Output | [bold]L[/]: Build Log | [bold]K[/]: Kill | [bold]Q[/]: Quit[/]");

        return table;
    }

    private string GetProjectName(ManagedProject mp) => mp.Name;

    private string GetAnimatedStatusMarkup(ManagedProject mp)
    {
        // stager in the frame for visual variety
        int frameIndex = animationFrame + (mp.GetHashCode() % 10);

        if (mp.IsBuilding)
        {
            var spinner = buildFrames[frameIndex % spinnerFrames.Length];
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

    private string GetBuildOutputContent(ManagedProject project)
    {
        if (string.IsNullOrEmpty(project.BuildOutput))
            return "[dim]No build output available.[/]";
            
        // Truncate if too long and highlight errors
        var lines = project.BuildOutput.Split('\n');
        if (lines.Length > 30)
        {
            var truncated = lines.TakeLast(30);
            return "[dim]... (truncated) ...[/]\n" + 
                   string.Join("\n", truncated.Select(FormatBuildLine));
        }
        
        return string.Join("\n", lines.Select(FormatBuildLine));
    }

    private string FormatBuildLine(string line)
    {
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
            return $"[red]{line}[/]";
        if (line.Contains("warning", StringComparison.OrdinalIgnoreCase))
            return $"[yellow]{line}[/]";
        if (line.Contains("succeeded", StringComparison.OrdinalIgnoreCase))
            return $"[green]{line}[/]";
        return line;
    }

    private void StopProject(ManagedProject managedProject)
    {
        managedProject.RunProcess?.Kill();
        managedProject.BuildProcess?.Kill();
        managedProject.BuildFailure = false;
    }

    private void ShowHelpDialog()
    {
        AnsiConsole.Clear();

        var helpTable = new Table();
        helpTable.AddColumn("[bold]Hotkey[/]");
        helpTable.AddColumn("[bold]Action[/]");
        helpTable.Border(TableBorder.Rounded);
        helpTable.Title("[bold cyan]Hotkey Help[/]");

        helpTable.AddRow("[bold blue]↑/↓[/]", "Navigate up/down through projects");
        helpTable.AddRow("[bold green]B[/]", "Build selected project");
        helpTable.AddRow("[bold green]R[/]", "Run selected project");
        helpTable.AddRow("[bold green]P[/]", "Build and Run selected project");
        helpTable.AddRow("[bold yellow]O[/]", "Show live output of selected project");
        helpTable.AddRow("[bold yellow]L[/]", "Show last build output of selected project");
        helpTable.AddRow("[bold red]K[/]", "Kill/Stop selected project");
        helpTable.AddRow("[bold magenta]T[/]", "Toggle live output for selected project");
        helpTable.AddRow("[bold cyan]H[/]", "Show this help");
        helpTable.AddRow("[bold red]Q/Esc[/]", "Quit interactive mode");

        AnsiConsole.Write(helpTable);
        AnsiConsole.WriteLine("\n[dim]Press any key to return...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    public void Stop()
    {
        isRunning = false;
    }
}