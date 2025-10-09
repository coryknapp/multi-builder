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

    static List<Command> commands = new List<Command>
    {
        new Command()
        {
            Invocations = new List<string>{"help", "h", ""},
            HelpString = "Display this help message.",
            Action = (CommandParameters _) => {InvokePrintHelpCommand(); },
        },
        new Command()
        {
            Invocations = new List<string>{"status", "s"},
            HelpString = "Display status of individual processes.",
            Action = (CommandParameters _) => {InvokePrintStatusCommand(); },
        },
        new Command()
        {
            Invocations = new List<string>{"stop", "st", "kill", "k"},
            HelpString = "Stops running projects",
            Action = (CommandParameters parameters) => {InvokeStopProjectsCommand(parameters); },
        },
        new Command()
        {
            Invocations = new List<string>{"build", "b"},
            HelpString = "Builds projects",
            Action = (CommandParameters parameters) => {InvokeBuildProjectsCommand(parameters); },
        },
        new Command()
        {
            Invocations = new List<string>{"run", "r"},
            HelpString = "Runs projects",
            Action = (CommandParameters parameters) => {InvokeRunProjectsCommand(parameters); },
        },
        //new Command()
        //{
        //    Invocations = new List<string>{"disable", "d"},
        //    HelpString = "Disables a process from being built and run",
        //    Action = (CommandParameters parameters) => {InvokePrintStatusCommand(); },
        //},
        new Command()
        {
            Invocations = new List<string>{"lastbuild", "lb", "buildoutput", "bo"},
            HelpString = "Prints the last build output for each project.",
            Action = (CommandParameters parameters) => { InvokePrintLastBuildOutputCommand(parameters); },
        },
        new Command()
        {
            Invocations = new List<string> { "run-output", "o" },
            HelpString = "Dumps the live output of a running process.",
            Action = (CommandParameters parameters) => { InvokePrintLiveProcessOutputCommand(parameters); },
        },
        new Command()
        {
            Invocations = new List<string> { "turn-on-run-output", "ro" },
            HelpString = "Selected process dump output in real time.",
            Action = (CommandParameters parameters) => { InvokeTurnOnOutputCommand(parameters); },
        },
        new Command()
        {
            Invocations = new List<string> { "turn-off-run-output", "roo" },
            HelpString = "Selected process stop dumping output in real time.",
            Action = (CommandParameters parameters) => { InvokeTurnOffOutputCommand(parameters); },
        },
    };

    private static void InvokeTurnOnOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, (mp) =>
        {
            mp.PrintOutputInRealTime = true;
        });

    private static void InvokeTurnOffOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, (mp) => mp.PrintOutputInRealTime = false);


    private static void InvokeRunProjectsCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, RunProject);

    private static async Task InvokeBuildProjectsCommand(CommandParameters parameters)
    {
        InvokeForEachProject(parameters, p =>
        {
            if (BuildQueue.Contains(p))
            {
                WriteErrorLine($"{p.Name} is already in the build queue.");
                return;
            }
            p.BuildFailure = false;
            p.RetryAttempts = 0;
            p.RetryEligible = false;
            p.LastBuildOutput = string.Empty;
            p.ErrorMessages = Array.Empty<string>();
            BuildQueue.Enqueue(p);
            PrintBuildQueueMessage = true;
        });

    }

    private static void InvokeStopProjectsCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, StopProject);
    private static void StopProject(ManagedProject managedProject)
    {
        managedProject.RunProcess?.Kill();
        managedProject.BuildProcess?.Kill();
        managedProject.BuildFailure = false;
    }

    private static List<int> GetAllProjectNumbers() =>
        Enumerable.Range(1, ManagedProjects.Count).ToList();

    private static void InvokePrintHelpCommand()
    {
        foreach (var cmd in commands)
        {
            Console.WriteLine($"{string.Join(", ", cmd.Invocations)}: {cmd.HelpString}");
        }
    }

    static void InvokePrintStatusCommand()
    {
        WriteHeaderLine("Current running processes:");
        int index = 1;
        foreach (var mp in ManagedProjects)
        {
            if (mp.IsBuilding)
            {
                WriteBuildingLine($"{index++}: {mp.Name} (Building)");
            }
            else if (mp.IsRunning)
            {
                WriteSuccessLine($"{index++}: {mp.Name} (Running)");
            }
            else if (mp.BuildFailure)
            {
                WriteErrorLine($"{index++}: {mp.Name} (Failed)");
            }
            else if (mp.LastBuildTime == null)
            {
                Console.WriteLine($"{index++}: {mp.Name} (never built)");
            }
            else
            {
                Console.WriteLine($"{index++}: {mp.Name} (built success at ${mp.LastBuildTime})");
            }
        }
    }

    private static void InvokePrintLastBuildOutputCommand(CommandParameters parameters) =>
                InvokeForEachProject(parameters, PrintLastBuildOutput);

    private static void InvokePrintLiveProcessOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, PrintLiveProcessOutput);

    private static void InvokeForEachProject(CommandParameters parameters, Action<ManagedProject> action)
    {
        foreach (var i in parameters.ProjectNumbers)
        {
            if(i > 0 && i <= ManagedProjects.Count)
            {
                action(ManagedProjects[i - 1]);
            }
            else
            {
                WriteErrorLine($"No project corresponds to {i}");
            }
        }
    }
}
