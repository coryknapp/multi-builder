using System.CommandLine;
using System.CommandLine.Invocation;

partial class Program
{
    static List<string> Directories;

    static List<ManagedProject> ManagedProjects;

    static Queue<ManagedProject> BuildQueue = new Queue<ManagedProject>();

    static bool PrintBuildQueueMessage = false;

    static string BuildCommand = "dotnet build -c Debug";

    static string RunCommand = "dotnet run --no-build --no-restore";

    static int ConcurrentBuildProcesses = 4;

    static int MaxRetryAtempts = 4;
    static bool DumpBuildOutputToFile { get; set; } = false; // Set to true to enable dumping

    static SemaphoreSlim BuildQueueSemaphore = new SemaphoreSlim(ConcurrentBuildProcesses);

    static async Task Main(string[] args)
    {
        ParseOptions(args);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => PrepareToExit();

        InitalizeManagedProcessesDictionary();

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
                WriteErrorLine($"Failed to kill process for {mp.Name}: {ex.Message}");
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

    static void WriteErrorLine(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    static void WriteSuccessLine(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    static void WriteBuildingLine(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    static void WriteHeaderLine(string message)
    {
        var originalBackground = Console.BackgroundColor;
        var originalForeground = Console.ForegroundColor;
        Console.BackgroundColor = ConsoleColor.Cyan;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($" --- {message} ---");
        Console.BackgroundColor = originalBackground;
        Console.ForegroundColor = originalForeground;
        Console.WriteLine();
    }

    private static void PrintLastBuildOutput(ManagedProject managedProject)
    {
        WriteHeaderLine($"Last build output for {managedProject.Name}");
        if (!string.IsNullOrEmpty(managedProject.LastBuildOutput))
        {
            Console.WriteLine(managedProject.LastBuildOutput);
        }
        else
        {
            Console.WriteLine("No build output available.");
        }
    }

    private static void PrintLiveProcessOutput(ManagedProject managedProject)
    {
        if (managedProject.LiveOutput.Count > 0)
        {
            WriteHeaderLine($"Live output for {managedProject.Name}");
            foreach (var line in managedProject.LiveOutput)
            {
                Console.WriteLine(line);
            }
        }
        else
        {
            Console.WriteLine("No output available yet.");
        }
    }
}
