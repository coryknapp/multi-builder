using System.Diagnostics;
using System.Text.RegularExpressions;

public class BuildService
{
    private readonly OptionService OptionService;

    private Queue<ManagedProject> BuildQueue = new Queue<ManagedProject>();
    private readonly SemaphoreSlim BuildQueueSemaphore;

    public event EventHandler BuildStarted;
    public event EventHandler BuildComplete;
    public event EventHandler BuildQueueEmpty;
    public event EventHandler BuildFailed;
    public event EventHandler BuildRetried;
    public event EventHandler OutputFileWritten;

    public BuildService(OptionService optionService)
    {
        OptionService = optionService;
        BuildQueueSemaphore = new SemaphoreSlim(OptionService.ConcurrentBuildProcesses);
    }

    public async Task StartBuildQueueProcessing()
    {
        var runningTasks = new HashSet<Task>();
        bool wasEmpty = true; // Track previous state to avoid duplicate events

        while (true)
        {
            // Clean up completed tasks
            runningTasks.RemoveWhere(t => t.IsCompleted);

            // Start new builds if queue has items and we have capacity
            while (BuildQueue.Count > 0 && runningTasks.Count < OptionService.ConcurrentBuildProcesses)
            {
                await BuildQueueSemaphore.WaitAsync();
                var mp = BuildQueue.Dequeue();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await BuildProject(mp);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        BuildQueueSemaphore.Release();
                    }
                });

                runningTasks.Add(task);
                wasEmpty = false; // We now have work
            }

            // Check if we're truly empty: no queue items AND no running builds
            bool isEmpty = BuildQueue.Count == 0 && runningTasks.Count == 0;

            if (isEmpty && !wasEmpty)
            {
                // Queue just became empty - fire the event
                BuildQueueEmpty?.Invoke(this, EventArgs.Empty);
                wasEmpty = true;
            }
            else if (!isEmpty)
            {
                wasEmpty = false;
            }

            await Task.Delay(500);
        }
    }

    public void EnqueueBuild(ManagedProject managedProject)
    {
        if (BuildQueue.Contains(managedProject))
        {
            return;
        }
        managedProject.BuildFailure = false;
        managedProject.RetryAttempts = 0;
        managedProject.BuildOutput = null;
        managedProject.ErrorMessages = Array.Empty<string>();
        BuildQueue.Enqueue(managedProject);
    }

    public bool IsProjectEnqueued(ManagedProject project)
    {
        return BuildQueue.Contains(project);
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

        return ("Unknown", string.Empty, line);
    }

    private async Task BuildProject(ManagedProject managedProject)
    {
        BuildStarted?.Invoke(this, new BuildEventArgs(managedProject));
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {OptionService.BuildCommand}",
            WorkingDirectory = managedProject.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi };
        managedProject.BuildProcess = process;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        managedProject.BuildOutput = output + Environment.NewLine + error;

        if (OptionService.DumpBuildOutputToFile)
        {
            var logFile = Path.Combine(managedProject.WorkingDirectory, $"{managedProject.Name}_build.log");
            await File.WriteAllTextAsync(logFile, managedProject.BuildOutput);
            this.OutputFileWritten?.Invoke(this, new OuputFileEventArgs(logFile));
        }

        if (process.ExitCode == 1)
        {
            managedProject.BuildFailure = true;
            managedProject.RetryAttempts++;
            managedProject.ErrorMessages = ProcessOutputForErrors(output);

            this.BuildFailed?.Invoke(this, new BuildEventArgs(managedProject));
            
            if (IsContentiousResourceFailure(managedProject) && managedProject.RetryAttempts <= OptionService.MaxRetryAtempts)
            {
                this.BuildRetried?.Invoke(this, new RetryEventArgs(managedProject, managedProject.RetryAttempts, OptionService.MaxRetryAtempts));
                BuildQueue.Enqueue(managedProject);
                return;
            }
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

public class RetryEventArgs : BuildEventArgs
{
    public int FailCount { get; private set; }

    public int MaxFailCount { get; private set; }

    public RetryEventArgs(ManagedProject managedProject, int failCount, int maxFailCount)
        : base(managedProject)
    {
        FailCount = failCount;
        MaxFailCount = maxFailCount;
    }
}
