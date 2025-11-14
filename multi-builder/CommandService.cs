using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CommandService
{
    public enum CommandResult
    {
        Exit, Continue
    }

    public TextService TextService;
    public RunService RunService;
    public BuildService BuildService;
    public BuildRunService BuildRunService;
    public OutputService OutputService;

    private List<Command> Commands;

    public CommandService(
        TextService textService,
        RunService runService,
        BuildService buildService,
        BuildRunService buildRunService,
        OutputService outputService)
    {
        TextService = textService;
        RunService = runService;
        BuildService = buildService;
        BuildRunService = buildRunService;
        OutputService = outputService;

        InitializeCommands();
    }

    public CommandResult ProcessCommand(string input)
    {
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
                if (int.TryParse(part, out int number) && number > 0 && number <= Program.ManagedProjects.Count)
                {
                    projectNumbers.Add(number);
                }
            }
            parameters.ProjectNumbers = projectNumbers;
        }

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Exit;
        }
        else
        {
            var command = this.Commands.Where(c => c.Invocations.Contains(commandString)).FirstOrDefault();
            if (command == null)
            {
                OutputService.PrintHelpMessage(this.Commands);
            }
            else
            {
                command.Action.Invoke(parameters);
            }
        }

        return CommandResult.Continue;
    }

    private void InitializeCommands()
    {
        this.Commands = new List<Command>
        {
            new Command()
            {
                Invocations = new List<string>{"help", "h", ""},
                HelpString = "Display this help message.",
                Action = (CommandParameters _) => {OutputService.PrintHelpMessage(this.Commands); },
            },
            new Command()
            {
                Invocations = new List<string>{"status", "s"},
                HelpString = "Display status of individual processes.",

                Action = (CommandParameters _) => OutputService.PrintStatus(Program.ManagedProjects),
            },
            new Command()
            {
                Invocations = new List<string>{"kill", "k"},
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
            new Command()
            {
                Invocations = new List<string>{"build-run", "br"},
                HelpString = "Builds and then runs the projects",
                Action = (CommandParameters parameters) => {InvokeBuildRunCommand(parameters); },
            },
            new Command()
            {
                Invocations = new List<string>{"buildoutput", "bo"},
                HelpString = "Prints the last build output for each project.",
                Action = (CommandParameters parameters) => { InvokePrintLastBuildOutputCommand(parameters); },
            },
            new Command()
            {
                Invocations = new List<string> { "run-output", "ro" },
                HelpString = "Dumps the live output of a running process.",
                Action = (CommandParameters parameters) => { InvokePrintLiveProcessOutputCommand(parameters); },
            },
            new Command()
            {
                Invocations = new List<string> { "turn-on-live-run-output", "lo" },
                HelpString = "Selected process dump output in real time.",
                Action = (CommandParameters parameters) => { InvokeEnableLiveOutputCommand(parameters); },
            },
            new Command()
            {
                Invocations = new List<string> { "turn-off-live-run-output", "loo" },
                HelpString = "Selected process stop dumping output in real time.",
                Action = (CommandParameters parameters) => { InvokeDisableLiveOutputCommand(parameters); },
            },
        }; ;
    }

    private void InvokeBuildRunCommand(CommandParameters parameters)
    {
        InvokeForEachProject(parameters, (mp) => BuildRunService.BuildAndRunProject(mp));
    }

    private void InvokeEnableLiveOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, (mp) => OutputService.EnableLiveRunOutput(mp));

    private void InvokeDisableLiveOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, (mp) => mp.PrintOutputInRealTime = false);


    private void InvokeRunProjectsCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, RunService.RunProject);

    private async Task InvokeBuildProjectsCommand(CommandParameters parameters)
    {
        InvokeForEachProject(parameters, p => BuildService.EnqueueBuild(p));
    }

    private void InvokeStopProjectsCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, StopProject);

    private void StopProject(ManagedProject managedProject)
    {
        managedProject.RunProcess?.Kill();
        managedProject.BuildProcess?.Kill();
        managedProject.BuildFailure = false;
    }

    private List<int> GetAllProjectNumbers() =>
        Enumerable.Range(1, Program.ManagedProjects.Count).ToList();

    private void InvokePrintLastBuildOutputCommand(CommandParameters parameters) =>
                InvokeForEachProject(parameters, OutputService.PrintBuildOutput);

    private void InvokePrintLiveProcessOutputCommand(CommandParameters parameters) =>
        InvokeForEachProject(parameters, OutputService.PrintRunOutput);

    private void InvokeForEachProject(CommandParameters parameters, Action<ManagedProject> action)
    {
        foreach (var i in parameters.ProjectNumbers)
        {
            if (i > 0 && i <= Program.ManagedProjects.Count)
            {
                action(Program.ManagedProjects[i - 1]);
            }
            else
            {
                TextService.WriteErrorLine($"No project corresponds to {i}");
            }
        }
    }
}
