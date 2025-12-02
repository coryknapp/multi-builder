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

    private int selectedIndex = 0;
    private bool isRunning = false;
    private LiveDisplayContext liveContext;
    private int animationFrame = 0; // Add animation frame counter

    // Animation frames for different states
    private readonly string[] spinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private readonly string[] dotsAnimation = { "⠀", "⠁", "⠃", "⠇", "⠏", "⠟", "⠿", "⡿", "⣿", "⣾", "⣼", "⣸", "⢸", "⠸", "⠘", "⠈" };
    private readonly string[] buildFrames = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂" };


    public InteractiveService(
        BuildService buildService,
        RunService runService,
        BuildRunService buildRunService)
    {
        this.buildService = buildService;
        this.runService = runService;
        this.buildRunService = buildRunService;
    }

    public async Task StartInteractiveMode(IList<ManagedProject> managedProjects, CancellationToken cancellationToken = default)
    {
        isRunning = true;
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
                        animationFrame++; // Increment animation frame
                        UpdateDisplay(managedProjects);
                        await Task.Delay(200, cancellationToken); // Faster updates for animation (200ms instead of 1000ms)
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
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            buildService.EnqueueBuild(project);
                        }
                        break;

                    case ConsoleKey.R: // Run
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            runService.RunProject(project);
                        }
                        break;

                    case ConsoleKey.P: // Build and Run (P for "Play")
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            _ = Task.Run(() => buildRunService.BuildAndRunProject(project));
                        }
                        break;

                    case ConsoleKey.O: // Show Output
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            ShowProjectOutput(project);
                        }
                        break;

                    case ConsoleKey.L: // Show Last build output
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            ShowBuildOutput(project);
                        }
                        break;

                    case ConsoleKey.K: // Kill/Stop
                        if (selectedIndex < managedProjects.Count)
                        {
                            var project = managedProjects[selectedIndex];
                            StopProject(project);
                        }
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
                $"{rowStyle}{GetLastBuildMarkup(mp)}{endStyle}"
            );
        }

        // Add hotkey instructions
        table.Caption("[dim]↑↓: Select | [bold]B[/]: Build | [bold]R[/]: Run | [bold]P[/]: Build+Run | [bold]O[/]: Output | [bold]L[/]: Build Log | [bold]K[/]: Kill | [bold]Q[/]: Quit[/]");

        return table;
    }

    private string GetProjectName(ManagedProject mp)
    {
        if (!mp.Enabled) return $"[dim]{mp.Name}[/]";
        return mp.Name;
    }

    private string GetAnimatedStatusMarkup(ManagedProject mp)
    {
        if (!mp.Enabled) return "[dim]Disabled[/]";
        
        if (mp.IsBuilding)
        {
            var spinner = buildFrames[animationFrame % spinnerFrames.Length];
            return $"[yellow]{spinner} Building[/]";
        }
        
        if (mp.IsRunning)
        {
            var dots = dotsAnimation[animationFrame % dotsAnimation.Length];
            return $"[green]{dots} Running[/]";
        }
        
        if (mp.BuildFailure) return "[red]❌ Failed[/]";
        if (mp.LastBuildTime.HasValue) return "[green]✅ Ready[/]";
        return "[dim]Not Built[/]";
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
        AnsiConsole.Clear();
        
        // Display the live output with formatting
        var panel = new Panel(GetProjectOutputContent(project))
            .Header($"[bold green]Live Output: {project.Name}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);
            
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine("\n[dim]Press any key to return...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    private string GetProjectOutputContent(ManagedProject project)
    {
        if (project.LiveOutput.Count == 0)
            return "[dim]No output available yet.[/]";
            
        var recent = project.LiveOutput.TakeLast(20); // Show last 20 lines
        return string.Join("\n", recent.Select(line => 
            string.IsNullOrWhiteSpace(line) ? " " : line));
    }

    private void ShowBuildOutput(ManagedProject project)
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(GetBuildOutputContent(project))
            .Header($"[bold cyan]Build Output: {project.Name}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(project.BuildFailure ? Color.Red : Color.Cyan);
            
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine("\n[dim]Press any key to return...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    private string GetBuildOutputContent(ManagedProject project)
    {
        if (string.IsNullOrEmpty(project.LastBuildOutput))
            return "[dim]No build output available.[/]";
            
        // Truncate if too long and highlight errors
        var lines = project.LastBuildOutput.Split('\n');
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