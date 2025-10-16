using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

class InteractiveSession
{
    private readonly BuildService BuildService;
    private readonly RunService RunService;
    private readonly OutputService OutputService;

    private IList<ManagedProject> ManagedProjects;
    private ConsoleColor OriginalBackground;
    private ConsoleColor OriginalForeground;
    private int SelectedIndex = 0;

    private ISet<ConsoleKey> UpKeys = new HashSet<ConsoleKey>() { ConsoleKey.UpArrow, ConsoleKey.K };
    private ISet<ConsoleKey> DownKeys = new HashSet<ConsoleKey>() { ConsoleKey.DownArrow, ConsoleKey.J };
    private ISet<ConsoleKey> ExitKeys = new HashSet<ConsoleKey>() { ConsoleKey.Escape, ConsoleKey.Q };
    private ISet<ConsoleKey> BuildKeys = new HashSet<ConsoleKey>() { ConsoleKey.B };
    private ISet<ConsoleKey> RunKeys = new HashSet<ConsoleKey>() { ConsoleKey.R, ConsoleKey.RightArrow };
    private ISet<ConsoleKey> KillKeys = new HashSet<ConsoleKey>() { ConsoleKey.K, ConsoleKey.LeftArrow };

    private ICollection<Task> Tasks = new Collection<Task>();

    public InteractiveSession(BuildService buildService, RunService runService, OutputService outputService)
    {
        BuildService = buildService;
        RunService = runService;
        OutputService = outputService;

        ManagedProjects = Program.ManagedProjects;

        OriginalBackground = Console.BackgroundColor;
        OriginalForeground = Console.ForegroundColor;
    }

    public async Task StartInteractiveSession()
    {
        while (true)
        {
            Console.Clear();
            DrawMenu();

            // Poll for key input with timeout
            ConsoleKeyInfo? key = null;
            var timeout = DateTime.Now.AddMilliseconds(1000); // Refresh every 500ms

            while (DateTime.Now < timeout)
            {
                if (Console.KeyAvailable)
                {
                    key = Console.ReadKey(true);
                    break;
                }
                Thread.Sleep(50); // Small delay to prevent high CPU usage
            }

            // If no key was pressed, continue loop (refresh display)
            if (key == null) continue;

            // Handle the key press
            HandleKeyPress(key.Value);
        }
    }

    private void HandleKeyPress(ConsoleKeyInfo key)
    {
        var commandParameters = new CommandParameters();
        if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
        {
            commandParameters.ProjectNumbers = Program.GetAllProjectNumbers();
        }
        else
        {
            commandParameters.ProjectNumbers = new List<int> { SelectedIndex + 1 };
        };

        if (UpKeys.Contains(key.Key))
        {
            SelectedIndex = (SelectedIndex - 1 + ManagedProjects.Count) % ManagedProjects.Count;
        }
        else if (DownKeys.Contains(key.Key))
        {
            SelectedIndex = (SelectedIndex + 1 + ManagedProjects.Count) % ManagedProjects.Count;
        }
        else if (BuildKeys.Contains(key.Key))
        {
            Program.InvokeForEachProject(commandParameters, p => BuildService.EnqueueBuild(p));
        }
        else if (RunKeys.Contains(key.Key))
        {
            Program.InvokeForEachProject(commandParameters, p => RunService.RunProject(p));
        }
        else if (KillKeys.Contains(key.Key))
        {
            Program.InvokeForEachProject(commandParameters, Program.StopProject);
        }
        else if (ExitKeys.Contains(key.Key))
        {
            EndInteractiveSession();
        }
    }

    private void EndInteractiveSession()
    {
        Console.Clear();

        Console.BackgroundColor = OriginalBackground;
        Console.ForegroundColor = OriginalForeground;
    }

    private void DrawMenu()
    {
        foreach (var (i, project) in ManagedProjects.Index())
        {
            DrawProjectStatusLine(i, project);
        }
    }

    private void DrawProjectStatusLine(int index, ManagedProject project)
    {
        Console.BackgroundColor = ProjectStatusBackGroundColor(index);
        Console.ForegroundColor = ProjectStatusForGroundColor(index, project);
        Console.WriteLine(ProjectStatusString(index, project));
        Console.BackgroundColor = ConsoleColor.Black;
    }

    private string ProjectStatusString(int index, ManagedProject project)
    {
        if (project.IsBuilding)
        {
            return $"{index++}: {project.Name} (Building)";
        }
        else if (project.IsRunning)
        {
            return $"{index++}: {project.Name} (Running)";
        }
        else if (project.BuildFailure)
        {
            return $"{index++}: {project.Name} (Failed)";
        }
        else if (project.LastBuildTime == null)
        {
            return $"{index++}: {project.Name} (never built)";
        }
        else
        {
            return $"{index++}: {project.Name} (built success at ${project.LastBuildTime})";
        }
    }

    private ConsoleColor ProjectStatusBackGroundColor(int index) =>
        index == SelectedIndex ? ConsoleColor.Yellow : ConsoleColor.Black;

    private ConsoleColor ProjectStatusForGroundColor(int index, ManagedProject project)
    {
        if (index == SelectedIndex)
            return ConsoleColor.Black;
        if (!project.Enabled)
            return ConsoleColor.DarkGray;
        if (project.IsBuilding)
            return ConsoleColor.Cyan;
        if (project.BuildFailure)
            return ConsoleColor.Red;
        if (project.IsRunning)
            return ConsoleColor.Green;
        return ConsoleColor.Gray;
    }
}
