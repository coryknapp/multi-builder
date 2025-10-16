using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;

public partial class Program
{
    static List<string> Directories;

    public static bool StartInInteractiveMode { get; private set; }

    static public List<ManagedProject> ManagedProjects;

    static Queue<ManagedProject> BuildQueue = new Queue<ManagedProject>();

    static bool PrintBuildQueueMessage = false;

    static public string BuildCommand = "dotnet build -c Debug";

    static public string RunCommand = "dotnet run --no-build --no-restore";

    static public int ConcurrentBuildProcesses = 4;

    static public int MaxRetryAtempts = 4;

    static public bool DumpBuildOutputToFile { get; set; } = false; // Set to true to enable dumping

    static SemaphoreSlim BuildQueueSemaphore = new SemaphoreSlim(ConcurrentBuildProcesses);

    static OutputService OutputService;

    static async Task Main(string[] args)
    {
        ParseOptions(args);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => PrepareToExit();

        var services = new ServiceCollection();
        services.AddSingleton<OutputService>();
        services.AddSingleton<InteractiveSession>();
        services.AddSingleton<ProcessManagerService>();
        services.AddSingleton<RunService>();
        services.AddSingleton<BuildService>();

        var serviceProvider = services.BuildServiceProvider();

        InitalizeManagedProcessesDictionary();

        OutputService = serviceProvider.GetRequiredService<OutputService>();

        if (StartInInteractiveMode)
        {
            BuildService buildService = serviceProvider.GetRequiredService<BuildService>();
            _ = buildService.StartBuildQueueProcessing();
            var session = serviceProvider.GetRequiredService<InteractiveSession>();
            session.StartInteractiveSession();
        }

        _ = CheckBuildQueue();

        // read user input loop
        while (true)
        {
            var input = Console.ReadLine();

            var splitList = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commandString = splitList.FirstOrDefault() ?? string.Empty;
            var parameters = new CommandParameters();
            if (splitList.Any(p => p == "-a"))
            {
                parameters.ProjectNumbers = GetAllProjectNumbers();
            }
            else
            {
                var projectNumbers = new List<int>();
                foreach (var part in splitList.Skip(1))
                {
                    if (int.TryParse(part, out int number) && number > 0 && number <= ManagedProjects.Count)
                    {
                        projectNumbers.Add(number);
                    }
                }
                parameters.ProjectNumbers = projectNumbers;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                PrepareToExit();
                break;
            }
            else
            {
                var command = commands.Where(c => c.Invocations.Contains(commandString)).FirstOrDefault();
                if (command == null)
                {
                    InvokePrintHelpCommand();
                }
                else
                {
                    command.Action.Invoke(parameters);
                }
            }
        }
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
                OutputService.WriteErrorLine($"Failed to kill process for {mp.Name}: {ex.Message}");
            }
        }
    }

    private static void InitalizeManagedProcessesDictionary()
    {
        ManagedProjects = Directories.Select(d => new ManagedProject
        {
            Name = Path.GetFileName(d),
            WorkingDirectory = d
        }).ToList();
    }

    private static void PrintLastBuildOutput(ManagedProject managedProject)
    {
        OutputService.WriteHeaderLine($"Last build output for {managedProject.Name}");
        if (!string.IsNullOrEmpty(managedProject.LastBuildOutput))
        {
            OutputService.WriteInfoLine(managedProject.LastBuildOutput);
        }
        else
        {
            OutputService.WriteInfoLine("No build output available.");
        }
    }

    private static void PrintLiveProcessOutput(ManagedProject managedProject)
    {
        if (managedProject.LiveOutput.Count > 0)
        {
            OutputService.WriteHeaderLine($"Live output for {managedProject.Name}");
            foreach (var line in managedProject.LiveOutput)
            {
                OutputService.WriteInfoLine(line);
            }
        }
        else
        {
            OutputService.WriteInfoLine("No output available yet.");
        }
    }
}
