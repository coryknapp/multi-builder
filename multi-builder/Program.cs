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
    static KillService KillService;

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
        services.AddSingleton<KillService>();
        services.AddSingleton<GitService>();

        var serviceProvider = services.BuildServiceProvider();
        
        OptionService = serviceProvider.GetRequiredService<OptionService>();
        OptionService.ParseOptions(args);

        BuildService = serviceProvider.GetRequiredService<BuildService>();
        InteractiveHotkeyService = serviceProvider.GetRequiredService<InteractiveService>();
        KillService = serviceProvider.GetRequiredService<KillService>();

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
            KillService.KillProject(mp);
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

    private static void InitalizeManagedProcessesDictionary()
    {
        ManagedProjects = OptionService.Directories.Select(d => new ManagedProject(Path.GetFileName(d), d)).ToList();
    }
}
