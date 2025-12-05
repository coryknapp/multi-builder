
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

public class OptionService
{
    public List<string> Directories { get; set; }

    public int ConcurrentBuildProcesses { get; set; } = 2;

    public int MaxRetryAtempts { get; set; } = 4;

    public int HideCursorSeconds { get; set; } = 5;

    public string BuildCommand { get; set; } = "dotnet build -c Debug";

    public string RunCommand { get; set; } = "dotnet run --no-build --no-restore";

    public bool DumpBuildOutputToFile { get; set; } = false;

    private static string ProgramDescription = "Multi-builder tool to manage building and running multiple projects concurrently.";

    public void ParseOptions(string[] args)
    {
        var directoriesOption = DirectoriesOption();
        var concurrentBuildProcessesOption = ConcurrentBuildProcessesOption();
        var hideCursorSecondsOption = HideCursorSecondsOption();
        var rootCommand = new RootCommand(OptionService.ProgramDescription)
        {
            directoriesOption,
            concurrentBuildProcessesOption,
            hideCursorSecondsOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            Directories = parseResult.GetValue(directoriesOption);
            ConcurrentBuildProcesses = parseResult.GetValue(concurrentBuildProcessesOption);
        });

        rootCommand.Parse(args).Invoke();
    }

    private Option<List<string>> DirectoriesOption() =>
        new("--directories")
        {
            Description = "List of project directories to manage",
            Required = true,
            Aliases = { "-d" },
            AllowMultipleArgumentsPerToken = true,
            Validators =
            {
                result =>
                {
                    var dirs = result.GetValueOrDefault<List<string>>();
                    if (dirs == null || dirs.Count == 0)
                    {
                        result.AddError( "At least one directory must be specified.");
                        return;
                    }
                    foreach (var dir in dirs)
                    {
                        if (!Directory.Exists(dir))
                        {
                            result.AddError($"Directory does not exist: {dir}");
                            return;
                        }
                    }
                }
            },
        };

    private Option<int> ConcurrentBuildProcessesOption() =>
        new("--concurrent-build-processes")
        {
            Description = "Number of allowed concurrent build processes",
            Required = false,
            Aliases = { "-c" },
            DefaultValueFactory = (_) => this.ConcurrentBuildProcesses,
        };

    private Option<int> HideCursorSecondsOption() =>
        new("--hide-cursor")
        {
            Description = "Set time before the cursor is hidden, in seconds.  Set to '0' to never hide.",
            Required = false,
            Aliases = { "-hr" },
            DefaultValueFactory = (_) => this.HideCursorSeconds,
        };
}
