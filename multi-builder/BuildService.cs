using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class BuildService
{
    private readonly OptionService OptionService;

    private Queue<ManagedProject> BuildQueue = new Queue<ManagedProject>();
    private readonly SemaphoreSlim BuildQueueSemaphore;
    
    public event EventHandler BuildStarted;
    public event EventHandler BuildComplete;
    public event EventHandler BuildFailed;
    public event EventHandler BuildRetried;
    public event EventHandler OutputFileWritten;

    public bool PrintBuildQueueMessage { get; private set; }

    public BuildService(OptionService optionService)
    {
        OptionService = optionService;
        BuildQueueSemaphore = new SemaphoreSlim(OptionService.ConcurrentBuildProcesses);
    }

    public async Task StartBuildQueueProcessing()
    {
        var buildTasks = new List<Task>();
        while (true)
        {
            while (BuildQueue.Count > 0)
            {
                await BuildQueueSemaphore.WaitAsync();
                var mp = BuildQueue.Dequeue();
                buildTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await BuildProject(mp);
                    }
                    finally
                    {
                        BuildQueueSemaphore.Release();
                    }
                }));

            };

            // TODO check build queue empty

            await Task.Delay(500);
        }
    }

    public async Task EnqueueBuild(ManagedProject managedProject)
    {
        if (BuildQueue.Contains(managedProject))
        {
            return;
        }
        managedProject.BuildFailure = false;
        managedProject.RetryAttempts = 0;
        managedProject.RetryEligible = false;
        managedProject.LastBuildOutput = string.Empty;
        managedProject.ErrorMessages = Array.Empty<string>();
        BuildQueue.Enqueue(managedProject);
        PrintBuildQueueMessage = true;
    }

    private static bool IsContentiousResourceFailure(ManagedProject managedProject)
    {
        var ignoredCodes = new List<string>()
        {
            "MSB3021", // Could not copy "x" to "y".
            "MSB3027",
        };

        var parsedMessages = managedProject.ErrorMessages.Select(m => ParseBuildLine(m));
        var errorMessages = parsedMessages.Where(pm => pm.Value.Type == "Error");
        return parsedMessages.Where(pm => pm.Value.Type == "Error").All(pm => ignoredCodes.Contains(pm.Value.Code));
    }

    public static (string Type, string Code, string Message)? ParseBuildLine(string line)
    {
        var warningRegex = new Regex(@"^.*?:\s*warning\s+([A-Z0-9]+)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
        var errorRegex = new Regex(@"^.*?:\s*error\s+([A-Z0-9]+)\s*:\s*(.*)$", RegexOptions.IgnoreCase);

        var warningMatch = warningRegex.Match(line);
        if (warningMatch.Success)
        {
            return ("Warning", warningMatch.Groups[1].Value, warningMatch.Groups[2].Value);
        }

        var errorMatch = errorRegex.Match(line);
        if (errorMatch.Success)
        {
            return ("Error", errorMatch.Groups[1].Value, errorMatch.Groups[2].Value);
        }

        return null;
    }

    private async Task BuildProject(ManagedProject managedProject)
    {
        BuildStarted.Invoke(this, new BuildEventArgs(managedProject));
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {OptionService.BuildCommand}",
            WorkingDirectory = managedProject.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        managedProject.BuildProcess = process;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        managedProject.LastBuildOutput = output + Environment.NewLine + error;

        if (OptionService.DumpBuildOutputToFile)
        {
            var logFile = Path.Combine(managedProject.WorkingDirectory, $"{managedProject.Name}_build.log");
            await File.WriteAllTextAsync(logFile, managedProject.LastBuildOutput);
            this.OutputFileWritten?.Invoke(this, new OuputFileEventArgs(logFile));
        }

        if (process.ExitCode == 1)
        {
            managedProject.BuildFailure = true;
            managedProject.RetryAttempts++;
            managedProject.ErrorMessages = ProcessOutputForErrors(output);
            if (IsContentiousResourceFailure(managedProject) && managedProject.RetryAttempts <= OptionService.MaxRetryAtempts)
            {
                this.BuildRetried?.Invoke(this, new BuildEventArgs(managedProject));
                BuildQueue.Enqueue(managedProject);
                this.PrintBuildQueueMessage = true;
                return;
            }
            this.BuildFailed?.Invoke(this, new BuildEventArgs(managedProject));
        }
        else
        {
            managedProject.BuildFailure = false;
            managedProject.LastBuildTime = DateTime.Now;
            this.BuildComplete?.Invoke(this, new BuildEventArgs(managedProject));
        }
    }

    private static IEnumerable<string> ProcessOutputForErrors(string output)
    {
        var lines = output.Split(output.Contains("\r\n") ? new[] { "\r\n" } : new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // find lines that indicate an error
        var afterFailed = lines
            .SkipWhile(line => !line.Contains("Build FAILED."))
            .Skip(1); // skip the "Build FAILED." line itself

        // last three lines are not errors
        return afterFailed.Take(Math.Max(0, afterFailed.Count() - 3));
    }
}

public class OuputFileEventArgs : EventArgs
{
    public string FilePath { get; }
    public OuputFileEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}

public class BuildEventArgs : EventArgs
{
    public ManagedProject ManagedProject { get; private set; }

    public BuildEventArgs(ManagedProject managedProject)
    {
        ManagedProject = managedProject;
    }
}
