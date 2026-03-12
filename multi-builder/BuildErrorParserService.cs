using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class BuildErrorParserService
{
    // MSBuild error format: path\to\file.cs(line,column): error CS1234: message
    // Also handles: path\to\file.cs(line): error CS1234: message
    private static readonly Regex ErrorPattern = new(
        @"^(?<file>.+?)\((?<line>\d+)(?:,(?<column>\d+))?\):\s*(?<type>error|warning|info)\s+(?<code>[A-Z0-9]+)\s*:\s*(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Simpler pattern for errors without file location
    private static readonly Regex SimpleErrorPattern = new(
        @"^\s*(?<type>error|warning)\s+(?<code>[A-Z0-9]+)\s*:\s*(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<BuildError> ParseBuildOutput(ManagedProject project)
    {
        var errors = new List<BuildError>();

        if (project.ErrorMessages == null)
            return errors;

        foreach (var line in project.ErrorMessages)
        {
            var parsed = ParseLine(line, project.Name);
            if (parsed != null && parsed.Type != BuildErrorType.Unknown)
            {
                errors.Add(parsed);
            }
        }

        return errors
            .OrderBy(e => e.Type) // Errors first, then warnings
            .ThenBy(e => e.FilePath)
            .ThenBy(e => e.Line)
            .ToList();
    }

    public IReadOnlyList<BuildError> ParseAllBuildLogs(ManagedProject project, LogCollectorService logCollector)
    {
        var errors = new List<BuildError>();
        var logs = logCollector.GetBuildLogs(project);

        foreach (var logLine in logs)
        {
            var parsed = ParseLine(logLine.Content, project.Name);
            if (parsed != null && parsed.Type != BuildErrorType.Unknown)
            {
                errors.Add(parsed);
            }
        }

        return errors
            .OrderBy(e => e.Type)
            .ThenBy(e => e.FilePath)
            .ThenBy(e => e.Line)
            .ToList();
    }

    public BuildError? ParseLine(string line, string projectName = "")
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // Try full pattern with file location
        var match = ErrorPattern.Match(line);
        if (match.Success)
        {
            return new BuildError
            {
                FilePath = match.Groups["file"].Value.Trim(),
                Line = int.Parse(match.Groups["line"].Value),
                Column = int.TryParse(match.Groups["column"].Value, out var col) ? col : 1,
                ErrorCode = match.Groups["code"].Value,
                Message = match.Groups["message"].Value.Trim(),
                Type = ParseErrorType(match.Groups["type"].Value),
                RawLine = line,
                ProjectName = projectName
            };
        }

        // Try simple pattern without file location
        var simpleMatch = SimpleErrorPattern.Match(line);
        if (simpleMatch.Success)
        {
            return new BuildError
            {
                ErrorCode = simpleMatch.Groups["code"].Value,
                Message = simpleMatch.Groups["message"].Value.Trim(),
                Type = ParseErrorType(simpleMatch.Groups["type"].Value),
                RawLine = line,
                ProjectName = projectName
            };
        }

        return null;
    }

    private static BuildErrorType ParseErrorType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "error" => BuildErrorType.Error,
            "warning" => BuildErrorType.Warning,
            "info" => BuildErrorType.Info,
            _ => BuildErrorType.Unknown
        };
    }
}