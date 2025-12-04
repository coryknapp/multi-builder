
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

public class OptionService
{
    public List<string> Directories { get; set; }

    public int ConcurrentBuildProcesses { get; set; }

    public int MaxRetryAtempts { get; set; } = 4;

    public string BuildCommand { get; set; } = "dotnet build -c Debug";

    public string RunCommand { get; set; } = "dotnet run --no-build --no-restore";

    public bool DumpBuildOutputToFile { get; set; } = false;

    public void ParseOptions(string[] args)
    {
        var directoriesOption = DirectoriesOption();
        var concurrentBuildProcessesOption = ConcurrentBuildProcessesOption();

        var rootCommand = new RootCommand("Multi-builder tool")
        {
            directoriesOption,
            concurrentBuildProcessesOption,
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
            DefaultValueFactory = (_) => 4,
        };
}
