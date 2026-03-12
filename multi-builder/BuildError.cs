using System;

/// <summary>
/// Represents a parsed build error or warning from MSBuild/dotnet build output.
/// </summary>
public class BuildError
{
    public string FilePath { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public BuildErrorType Type { get; init; }
    public string RawLine { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;

    public bool HasLocation => !string.IsNullOrEmpty(FilePath) && Line > 0;

    public string LocationDisplay => HasLocation
        ? $"{System.IO.Path.GetFileName(FilePath)}({Line},{Column})"
        : "Unknown location";

    public string FullLocationDisplay => HasLocation
        ? $"{FilePath}({Line},{Column})"
        : "Unknown location";
}

public enum BuildErrorType
{
    Error,
    Warning,
    Info,
    Unknown
}