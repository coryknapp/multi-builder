using System.CommandLine;
using System.IO;

partial class Program
{
    static void ParseOptions(string[] args)
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

    static Option<List<string>> DirectoriesOption() =>
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

    static Option<bool> InteractiveOption() =>
        new("--interactive")
        {
            Description = "Start in interactive mode",
            Required = false,
            Aliases = { "-i" },
        };
}
