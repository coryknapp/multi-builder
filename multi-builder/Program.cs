using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using static CommandService;

public class Program
{
    static TextService TextService;
    static BuildService BuildService;
    static OutputService OutputService;
    static CommandService CommandService;
    static OptionService OptionService;

    static public IList<ManagedProject> ManagedProjects;

    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<OptionService>();
        services.AddSingleton<TextService>();
        services.AddSingleton<RunService>();
        services.AddSingleton<BuildService>();
        services.AddSingleton<BuildRunService>();
        services.AddSingleton<BuildOutputService>();
        services.AddSingleton<OutputService>();
        services.AddSingleton<CommandService>();

        var serviceProvider = services.BuildServiceProvider();
        // OptionService needs to be initialized first to get options for other services
        OptionService = serviceProvider.GetRequiredService<OptionService>();
        TextService = serviceProvider.GetRequiredService<TextService>();
        BuildService = serviceProvider.GetRequiredService<BuildService>();
        _ = serviceProvider.GetRequiredService <BuildOutputService>(); //invoke the service so it can attach itself to the BuildService event hooks.
        OutputService = serviceProvider.GetRequiredService<OutputService>();
        CommandService = serviceProvider.GetRequiredService<CommandService>();

        OptionService.ParseOptions(args);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => PrepareToExit();

        InitalizeManagedProcessesDictionary();

        _ = BuildService.StartBuildQueueProcessing();
        
        ReadLine.HistoryEnabled = true;

        // read user input loop
        while (true)
        {
            string input = ReadLine.Read("> ");  // This handles everything automatically
            if (CommandService.ProcessCommand(input) == CommandResult.Exit)
                break;
        }

        PrepareToExit();
    }

    private static void PrepareToExit()
    {
        foreach (var mp in ManagedProjects)
        {
            try
            {
                if (mp.RunProcess != null && !mp.RunProcess.HasExited)
                    mp.RunProcess.Kill(true); // true = kill entire process tree

                if (mp.BuildProcess != null && !mp.BuildProcess.HasExited)
                    mp.BuildProcess.Kill(true);
            }
            catch (Exception ex)
            {
                TextService.WriteErrorLine($"Failed to kill process for {mp.Name}: {ex.Message}");
            }
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
