
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

public class OptionService
{
    public List<string> Directories { get; set; } = new();
    public bool StartInInteractiveMode { get; set; }
    public int ConcurrentBuildProcesses { get; set; } = 4;
    public int MaxRetryAtempts { get; set; } = 4;
    public string BuildCommand { get; set; } = "dotnet build -c Debug";
    public string RunCommand { get; set; } = "dotnet run --no-build --no-restore";
    public bool DumpBuildOutputToFile { get; set; } = false;

    public void ParseOptions(string[] args)
    {
        var directoriesOption = DirectoriesOption();
        var interactiveOption = InteractiveOption();

        var rootCommand = new RootCommand("Multi-builder tool")
        {
            directoriesOption,
            interactiveOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            Directories = parseResult.GetValue(directoriesOption);
            StartInInteractiveMode = parseResult.GetValue(interactiveOption);
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

    private Option<bool> InteractiveOption() =>
        new("--interactive")
        {
            Description = "Start in interactive mode",
            Required = false,
            Aliases = { "-i" },
        };
}
