using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Spectre.Console;

public class ErrorViewerService : IFullScreenView
{
    private readonly BuildErrorParserService errorParser;
    private readonly LogCollectorService logCollector;
    private readonly OptionService optionService;

    private IReadOnlyList<BuildError> errors = [];
    private ManagedProject? currentProject;
    private int selectedIndex = 0;
    private int scrollOffset = 0;
    private bool showWarnings = false;
    private bool shouldExit = false;

    private static readonly Color ErrorColor = Color.Red;
    private static readonly Color WarningColor = Color.Yellow;
    private static readonly Color SelectedBackground = Color.Blue;
    private static readonly Color EvenRowBackground = new(30, 30, 30);
    private static readonly Color OddRowBackground = new(40, 40, 40);

    public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(100);

    public ErrorViewerService(
        BuildErrorParserService errorParser,
        LogCollectorService logCollector,
        OptionService optionService)
    {
        this.errorParser = errorParser;
        this.logCollector = logCollector;
        this.optionService = optionService;
    }

    public void ShowErrors(ManagedProject project)
    {
        currentProject = project;
        selectedIndex = 0;
        scrollOffset = 0;
        shouldExit = false;
        RefreshErrors();
    }

    public void RefreshErrors()
    {
        if (currentProject == null) return;

        var allErrors = errorParser.ParseAllBuildLogs(currentProject, logCollector);

        errors = showWarnings
            ? allErrors
            : allErrors.Where(e => e.Type == BuildErrorType.Error).ToList();
    }

    public void OnActivated()
    {
        Console.CursorVisible = false;
        Console.Clear();
    }

    public void OnDeactivated()
    {
        Console.CursorVisible = true;
    }

    public void Render()
    {
        Console.SetCursorPosition(0, 0);

        int consoleWidth = Console.WindowWidth;
        int consoleHeight = Console.WindowHeight;
        int headerHeight = 5;
        int footerHeight = 3;
        int viewableLines = Math.Max(1, consoleHeight - headerHeight - footerHeight);

        RenderHeader(consoleWidth);
        EnsureSelectedVisible(viewableLines);
        RenderErrors(viewableLines, consoleWidth);
        RenderFooter(consoleWidth);
    }

    private void RenderHeader(int consoleWidth)
    {
        var projectName = currentProject?.Name ?? "Unknown";
        var rule = new Rule($"[bold red]Build Errors - {Markup.Escape(projectName)}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("red")
        };
        AnsiConsole.Write(rule);

        var errorCount = errors.Count(e => e.Type == BuildErrorType.Error);
        var warningCount = errors.Count(e => e.Type == BuildErrorType.Warning);

        AnsiConsole.MarkupLine($"[red]{errorCount} error(s)[/] | [yellow]{warningCount} warning(s)[/] | Showing: {(showWarnings ? "All" : "Errors only")}");

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Selected: {selectedIndex + 1}/{errors.Count}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No errors found![/]");
        }

        Console.WriteLine();
    }

    private void RenderErrors(int viewableLines, int consoleWidth)
    {
        for (int i = 0; i < viewableLines; i++)
        {
            int errorIndex = scrollOffset + i;

            if (errorIndex < errors.Count)
            {
                RenderErrorLine(errors[errorIndex], errorIndex, i, consoleWidth);
            }
            else
            {
                // Empty line
                var bg = i % 2 == 0 ? EvenRowBackground : OddRowBackground;
                AnsiConsole.Write(new Text(new string(' ', consoleWidth) + "\n", new Style(Color.White, bg)));
            }
        }
    }

    private void RenderErrorLine(BuildError error, int errorIndex, int displayIndex, int consoleWidth)
    {
        bool isSelected = errorIndex == selectedIndex;
        var background = isSelected ? SelectedBackground : (displayIndex % 2 == 0 ? EvenRowBackground : OddRowBackground);
        var typeColor = error.Type == BuildErrorType.Error ? ErrorColor : WarningColor;

        // Build the display line
        var typeIndicator = error.Type == BuildErrorType.Error ? "ERR" : "WRN";
        var location = error.HasLocation ? error.LocationDisplay : "";
        var code = error.ErrorCode;
        var message = error.Message;

        // Format: [ERR] CS1234 | File.cs(10,5) | Error message here...
        var prefix = $"[{typeIndicator}] {code,-8} | {location,-25} | ";
        var maxMessageLen = Math.Max(10, consoleWidth - prefix.Length - 1);
        var truncatedMessage = message.Length > maxMessageLen
            ? message[..(maxMessageLen - 3)] + "..."
            : message;

        var fullLine = prefix + truncatedMessage;
        var padding = Math.Max(0, consoleWidth - fullLine.Length - 1);
        var paddedLine = fullLine + new string(' ', padding) + "\n";

        // Use markup for the type indicator color
        if (isSelected)
        {
            AnsiConsole.Write(new Text(paddedLine, new Style(Color.White, background)));
        }
        else
        {
            // Render with colored type indicator
            AnsiConsole.Write(new Text($"[{typeIndicator}] ", new Style(typeColor, background)));
            AnsiConsole.Write(new Text($"{code,-8} | ", new Style(Color.Cyan1, background)));
            AnsiConsole.Write(new Text($"{location,-25} | ", new Style(Color.Grey, background)));

            var msgPadding = Math.Max(0, consoleWidth - prefix.Length - truncatedMessage.Length - 1);
            AnsiConsole.Write(new Text(truncatedMessage + new string(' ', msgPadding) + "\n", new Style(Color.White, background)));
        }
    }

    private void RenderFooter(int consoleWidth)
    {
        Console.WriteLine();
        AnsiConsole.MarkupLine("[dim]↑↓[/] Navigate  [dim]Enter[/] Open in VS  [dim]W[/] Toggle Warnings  [dim]R[/] Refresh  [dim]Esc/Q[/] Exit");

        if (selectedIndex >= 0 && selectedIndex < errors.Count)
        {
            var error = errors[selectedIndex];
            if (error.HasLocation)
            {
                AnsiConsole.MarkupLine($"[dim]Path: {Markup.Escape(error.FullLocationDisplay)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Full: {Markup.Escape(error.RawLine)}[/]");
            }
        }
    }

    private void EnsureSelectedVisible(int viewableLines)
    {
        if (selectedIndex < scrollOffset)
        {
            scrollOffset = selectedIndex;
        }
        else if (selectedIndex >= scrollOffset + viewableLines)
        {
            scrollOffset = selectedIndex - viewableLines + 1;
        }

        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, errors.Count - viewableLines));
    }

    public bool HandleKey(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                if (errors.Count > 0)
                    selectedIndex = Math.Max(0, selectedIndex - 1);
                break;

            case ConsoleKey.DownArrow:
                if (errors.Count > 0)
                    selectedIndex = Math.Min(errors.Count - 1, selectedIndex + 1);
                break;

            case ConsoleKey.PageUp:
                selectedIndex = Math.Max(0, selectedIndex - GetPageSize());
                break;

            case ConsoleKey.PageDown:
                selectedIndex = Math.Min(Math.Max(0, errors.Count - 1), selectedIndex + GetPageSize());
                break;

            case ConsoleKey.Home:
                selectedIndex = 0;
                scrollOffset = 0;
                break;

            case ConsoleKey.End:
                selectedIndex = Math.Max(0, errors.Count - 1);
                break;

            case ConsoleKey.Enter:
                OpenSelectedInVisualStudio();
                break;

            case ConsoleKey.W:
                showWarnings = !showWarnings;
                RefreshErrors();
                selectedIndex = Math.Min(selectedIndex, Math.Max(0, errors.Count - 1));
                break;

            case ConsoleKey.R:
                RefreshErrors();
                break;

            case ConsoleKey.Escape:
            case ConsoleKey.Q:
                return false;
        }

        return true;
    }

    private int GetPageSize() => Math.Max(1, Console.WindowHeight - 8);

    private void OpenSelectedInVisualStudio()
    {
        if (selectedIndex < 0 || selectedIndex >= errors.Count)
            return;

        var error = errors[selectedIndex];
        if (!error.HasLocation)
            return;

        try
        {
            // Use devenv.exe to open file at specific line
            // Format: devenv /edit "filepath" /command "Edit.GoTo lineNumber"
            var devenvPath = FindVisualStudioPath();
            if (string.IsNullOrEmpty(devenvPath))
            {
                // Fallback: try to open with VS Code
                OpenInVSCode(error);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = devenvPath,
                Arguments = $"/edit \"{error.FilePath}\" /command \"Edit.GoTo {error.Line}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to open VS: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private void OpenInVSCode(BuildError error)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"--goto \"{error.FilePath}:{error.Line}:{error.Column}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch
        {
            // Fallback: just open the file
            Process.Start(new ProcessStartInfo
            {
                FileName = error.FilePath,
                UseShellExecute = true
            });
        }
    }

    private string? FindVisualStudioPath()
    {
        // Common VS installation paths
        var possiblePaths = new[]
        {
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2026\Enterprise\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2026\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe",
            Environment.ExpandEnvironmentVariables(@"%VSAPPIDDIR%\devenv.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (System.IO.File.Exists(path))
                return path;
        }

        return null;
    }
}