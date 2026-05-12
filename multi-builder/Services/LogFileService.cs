using System.Diagnostics;

public class LogFileService
{
    private readonly LogCollectorService logCollectorService;
    private readonly string notepadPlusPlusPath;

    public LogFileService(LogCollectorService logCollectorService)
    {
        this.logCollectorService = logCollectorService;

        // Try common Notepad++ installation paths
        var possiblePaths = new[]
        {
            @"C:\Program Files\Notepad++\notepad++.exe",
            @"C:\Program Files (x86)\Notepad++\notepad++.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Notepad++\notepad++.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Notepad++\notepad++.exe")
        };

        notepadPlusPlusPath = possiblePaths.FirstOrDefault(File.Exists) ?? "notepad++.exe";
    }

    public void OpenBuildLog(ManagedProject project)
    {
        var logFile = GetBuildLogPath(project);
        WriteBuildLogToFile(project, logFile);
        OpenInNotepadPlusPlus(logFile);
    }

    public void OpenRunLog(ManagedProject project)
    {
        var logFile = GetRunLogPath(project);
        WriteRunLogToFile(project, logFile);
        OpenInNotepadPlusPlus(logFile);
    }

    private string GetBuildLogPath(ManagedProject project)
    {
        return Path.Combine(project.WorkingDirectory, $"{project.Name}_build.log");
    }

    private string GetRunLogPath(ManagedProject project)
    {
        return Path.Combine(project.WorkingDirectory, $"{project.Name}_run.log");
    }

    private void WriteBuildLogToFile(ManagedProject project, string logFile)
    {
        var logs = logCollectorService.GetBuildLogs(project);
        var logLines = logs.Select(l => $"[{l.Timestamp:HH:mm:ss.fff}] {l.Content}");
        File.WriteAllLines(logFile, logLines);
    }

    private void WriteRunLogToFile(ManagedProject project, string logFile)
    {
        var logs = logCollectorService.GetRunLogs(project);
        var logLines = logs.Select(l => $"[{l.Timestamp:HH:mm:ss.fff}] {l.Content}");
        File.WriteAllLines(logFile, logLines);
    }

    private void OpenInNotepadPlusPlus(string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = notepadPlusPlusPath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open Notepad++: {ex.Message}");
            Console.WriteLine($"Log file saved to: {filePath}");

            // Fallback to default text editor
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch
            {
                Console.WriteLine("Could not open log file with default editor.");
            }
        }
    }
}
