using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

partial class Program
{
    private static async Task CheckBuildQueue()
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

            if(PrintBuildQueueMessage)
            {
                PrintBuildQueueMessage = false;
                WriteSuccessLine("Build queue exhausted.");
            }

            await Task.Delay(500);
        }
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

    private static async Task BuildProject(ManagedProject managedProcess)
    {
        Console.WriteLine($"Running '{BuildCommand}' in '{managedProcess.Name}'");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {BuildCommand}",
            WorkingDirectory = managedProcess.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        managedProcess.BuildProcess = process;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        managedProcess.LastBuildOutput = output + Environment.NewLine + error;

        if (DumpBuildOutputToFile)
        {
            var logFile = Path.Combine(managedProcess.WorkingDirectory, $"{managedProcess.Name}_build.log");
            await File.WriteAllTextAsync(logFile, managedProcess.LastBuildOutput);
            WriteBuildingLine($"Build output dumped to: {logFile}");
        }

        if (process.ExitCode == 1)
        {
            managedProcess.BuildFailure = true;
            managedProcess.RetryAttempts++;
            managedProcess.ErrorMessages = ProcessOutputForErrors(output);
            if (IsContentiousResourceFailure(managedProcess) && managedProcess.RetryAttempts <= MaxRetryAtempts)
            {
                WriteErrorLine($"{managedProcess.Name} build failed due to contentious resource. Retrying attempt {managedProcess.RetryAttempts} of {MaxRetryAtempts}...");
                BuildQueue.Enqueue(managedProcess);
                PrintBuildQueueMessage = true;
                return;
            }
            WriteErrorLine($"{managedProcess.Name} build failed.");
        }
        else
        {
            managedProcess.BuildFailure = false;
            managedProcess.LastBuildTime = DateTime.Now;
            WriteSuccessLine($"{managedProcess.Name} built successfully.");
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
