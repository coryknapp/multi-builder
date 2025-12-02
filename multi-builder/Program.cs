using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;

public class Program
{
    static BuildService BuildService;
    static OptionService OptionService;
    static InteractiveService InteractiveHotkeyService;

    static public IList<ManagedProject> ManagedProjects;
    static private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<OptionService>();
        services.AddSingleton<RunService>();
        services.AddSingleton<BuildService>();
        services.AddSingleton<BuildRunService>();
        services.AddSingleton<InteractiveService>();

        var serviceProvider = services.BuildServiceProvider();
        
        OptionService = serviceProvider.GetRequiredService<OptionService>();
        OptionService.ParseOptions(args);

        BuildService = serviceProvider.GetRequiredService<BuildService>();
        InteractiveHotkeyService = serviceProvider.GetRequiredService<InteractiveService>();

        AppDomain.CurrentDomain.ProcessExit += (s, e) => PrepareToExit();

        InitalizeManagedProcessesDictionary();

        _ = BuildService.StartBuildQueueProcessing();

        await InteractiveHotkeyService.StartInteractiveMode(ManagedProjects, _cancellationTokenSource.Token);

        PrepareToExit();
    }

    private static void PrepareToExit()
    {
        _cancellationTokenSource?.Cancel();
        InteractiveHotkeyService?.Stop();
        
        foreach (var mp in ManagedProjects)
        {
            if (mp.RunProcess != null && !mp.RunProcess.HasExited)
                mp.RunProcess.Kill(true); // true = kill entire process tree

            if (mp.BuildProcess != null && !mp.BuildProcess.HasExited)
                mp.BuildProcess.Kill(true);
        }
    }

    private static void InitalizeManagedProcessesDictionary()
    {
        ManagedProjects = OptionService.Directories.Select(d => new ManagedProject
        {
            Name = Path.GetFileName(d),
            WorkingDirectory = d
        }).ToList();
    }
}
