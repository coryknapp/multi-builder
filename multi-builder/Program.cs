using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class Program
{
    static BuildService BuildService;
    static OptionService OptionService;
    static InteractiveService InteractiveHotkeyService;

    static public IList<ManagedProject> ManagedProjects;
    static private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    static private bool isExiting = false;
    private static object exitLock = new object();

    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<OptionService>();
        services.AddSingleton<RunService>();
        services.AddSingleton<BuildService>();
        services.AddSingleton<BuildRunService>();
        services.AddSingleton<InteractiveService>();
        services.AddSingleton<OutputService>();

        var serviceProvider = services.BuildServiceProvider();
        
        OptionService = serviceProvider.GetRequiredService<OptionService>();
        OptionService.ParseOptions(args);

        BuildService = serviceProvider.GetRequiredService<BuildService>();
        InteractiveHotkeyService = serviceProvider.GetRequiredService<InteractiveService>();

        AppDomain.CurrentDomain.ProcessExit += (s, e) => PrepareToExit();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            PrepareToExit();
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => PrepareToExit();

        InitalizeManagedProcessesDictionary();

        _ = BuildService.StartBuildQueueProcessing();

        await InteractiveHotkeyService.StartInteractiveMode(ManagedProjects, _cancellationTokenSource.Token);
    }

    private static void PrepareToExit()
    {
        lock (exitLock)
        {
            if (isExiting) return; // Prevent multiple cleanup attempts
            isExiting = true;
        }

        InteractiveHotkeyService?.Stop();

        var killTasks = new List<Task>();

        foreach (var mp in ManagedProjects)
        {
            // Kill run processes
            if (mp.RunProcess != null && !mp.RunProcess.HasExited)
            {
                killTasks.Add(Task.Run(() => KillProcessSafely(mp.RunProcess, $"Run process for {mp.Name}")));
            }

            // Kill build processes
            if (mp.BuildProcess != null && !mp.BuildProcess.HasExited)
            {
                killTasks.Add(Task.Run(() => KillProcessSafely(mp.BuildProcess, $"Build process for {mp.Name}")));
            }
        }

        // Wait for all kill operations with timeout
        try
        {
            Task.WaitAll(killTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex)
        {
            AnsiConsole.MarkupLine($"[red]Some processes could not be killed cleanly: {ex.Message}[/]");
        }
    }

    private static void KillProcessSafely(Process process, string description)
    {
        try
        {
            if (process == null || process.HasExited) return;

            AnsiConsole.MarkupLine($"[yellow]Terminating {description}...[/]");

            // Try graceful shutdown first (if the process supports it)
            try
            {
                process.CloseMainWindow();
                if (process.WaitForExit(2000)) // Wait 2 seconds for graceful exit
                {
                    AnsiConsole.MarkupLine($"[green]{description} exited gracefully[/]");
                    return;
                }
            }
            catch
            {
                // CloseMainWindow might fail, continue to Kill
            }

            // Force kill the entire process tree
            process.Kill(true); // true = kill entire process tree

            // Wait a bit to ensure it's dead
            if (process.WaitForExit(3000))
            {
                AnsiConsole.MarkupLine($"[green]{description} terminated[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]{description} did not respond to termination[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error killing {description}: {ex.Message}[/]");
        }
        finally
        {
            try
            {
                process?.Dispose();
            }
            catch { /* Ignore disposal errors */ }
        }
    }

    private static void InitalizeManagedProcessesDictionary()
    {
        ManagedProjects = OptionService.Directories.Select(d => new ManagedProject(Path.GetFileName(d), d)).ToList();
    }
}
