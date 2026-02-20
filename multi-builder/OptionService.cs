using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

public class OptionService
{
    public List<string> Directories { get; set; } = [];

    public int ConcurrentBuildProcesses { get; set; } = 2;

    public int MaxRetryAttempts { get; set; } = 4;

    public int HideCursorSeconds { get; set; } = 60 * 5;

    public string BuildCommand { get; set; } = "dotnet build -c Debug";

    public string RunCommand { get; set; } = "dotnet run --no-build --no-restore";

    public bool DumpBuildOutputToFile { get; set; } = false;

    public int MaxGitBranchLength { get; set; } = 32;

    private static readonly string ProgramDescription = "Multi-builder tool to manage building and running multiple projects concurrently.";
    private static readonly string DefaultConfigFileName = "multi-builder.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void ParseOptions(string[] args)
    {
        var configFileOption = ConfigFileOption();
        var directoriesOption = DirectoriesOption();
        var concurrentBuildProcessesOption = ConcurrentBuildProcessesOption();
        var hideCursorSecondsOption = HideCursorSecondsOption();
        var maxGitBranchLengthOption = MaxGitBranchLengthOption();
        var buildCommandOption = BuildCommandOption();
        var runCommandOption = RunCommandOption();
        var generateConfigOption = GenerateConfigOption();

        var rootCommand = new RootCommand(ProgramDescription)
        {
            configFileOption,
            directoriesOption,
            concurrentBuildProcessesOption,
            hideCursorSecondsOption,
            maxGitBranchLengthOption,
            buildCommandOption,
            runCommandOption,
            generateConfigOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            // Check if user wants to generate a sample config
            if (parseResult.GetValue(generateConfigOption))
            {
                GenerateSampleConfig();
                Environment.Exit(0);
            }

            // Load config file first (if specified or default exists)
            var configFile = parseResult.GetValue(configFileOption);
            LoadConfigFile(configFile);

            // CLI arguments override config file values
            if (parseResult.Tokens.Any(t => t.Value == "--directories" || t.Value == "-d"))
            {
                Directories = parseResult.GetValue(directoriesOption) ?? [];
            }

            if (parseResult.Tokens.Any(t => t.Value == "--concurrent-build-processes" || t.Value == "-c"))
            {
                ConcurrentBuildProcesses = parseResult.GetValue(concurrentBuildProcessesOption);
            }

            if (parseResult.Tokens.Any(t => t.Value == "--hide-cursor" || t.Value == "-hr"))
            {
                HideCursorSeconds = parseResult.GetValue(hideCursorSecondsOption);
            }

            if (parseResult.Tokens.Any(t => t.Value == "--git-branch-length"))
            {
                MaxGitBranchLength = parseResult.GetValue(maxGitBranchLengthOption);
            }

            if (parseResult.Tokens.Any(t => t.Value == "--build-command" || t.Value == "-bc"))
            {
                BuildCommand = parseResult.GetValue(buildCommandOption) ?? BuildCommand;
            }

            if (parseResult.Tokens.Any(t => t.Value == "--run-command" || t.Value == "-rc"))
            {
                RunCommand = parseResult.GetValue(runCommandOption) ?? RunCommand;
            }

            // If no directories from CLI, use config file directories
            // Validate directories exist
            ValidateDirectories();
        });

        rootCommand.Parse(args).Invoke();
    }

    private void LoadConfigFile(string? configPath)
    {
        // Try specified path, then default in current directory, then user profile
        var pathsToTry = new List<string>();

        if (!string.IsNullOrEmpty(configPath))
        {
            pathsToTry.Add(configPath);
        }

        pathsToTry.Add(Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName));
        pathsToTry.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultConfigFileName));

        foreach (var path in pathsToTry)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<ConfigFile>(json, JsonOptions);

                    if (config != null)
                    {
                        ApplyConfig(config);
                        Console.WriteLine($"Loaded config from: {path}");
                    }
                    return;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Warning: Failed to parse config file '{path}': {ex.Message}");
                }
            }
        }
    }

    private void ApplyConfig(ConfigFile config)
    {
        if (config.Directories?.Count > 0)
        {
            // Expand environment variables in paths
            Directories = config.Directories
                .Select(d => Environment.ExpandEnvironmentVariables(d))
                .ToList();
        }

        if (config.ConcurrentBuildProcesses.HasValue)
            ConcurrentBuildProcesses = config.ConcurrentBuildProcesses.Value;

        if (config.MaxRetryAttempts.HasValue)
            MaxRetryAttempts = config.MaxRetryAttempts.Value;

        if (config.HideCursorSeconds.HasValue)
            HideCursorSeconds = config.HideCursorSeconds.Value;

        if (!string.IsNullOrEmpty(config.BuildCommand))
            BuildCommand = config.BuildCommand;

        if (!string.IsNullOrEmpty(config.RunCommand))
            RunCommand = config.RunCommand;

        if (config.DumpBuildOutputToFile.HasValue)
            DumpBuildOutputToFile = config.DumpBuildOutputToFile.Value;

        if (config.MaxGitBranchLength.HasValue)
            MaxGitBranchLength = config.MaxGitBranchLength.Value;
    }

    private void ValidateDirectories()
    {
        if (Directories == null || Directories.Count == 0)
        {
            Console.WriteLine("Error: At least one directory must be specified (via --directories or config file).");
            Environment.Exit(1);
        }

        foreach (var dir in Directories)
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Error: Directory does not exist: {dir}");
                Environment.Exit(1);
            }
        }
    }

    private void GenerateSampleConfig()
    {
        var sampleConfig = new ConfigFile
        {
            Directories =
            [
                @"C:\Projects\MyApp.Web",
                @"C:\Projects\MyApp.API",
            ],
            ConcurrentBuildProcesses = 2,
            MaxRetryAttempts = 4,
            HideCursorSeconds = 300,
            BuildCommand = "dotnet build -c Debug",
            RunCommand = "dotnet run --no-build --no-restore",
            DumpBuildOutputToFile = false,
            MaxGitBranchLength = 32
        };

        var json = JsonSerializer.Serialize(sampleConfig, JsonOptions);
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Sample config file generated: {outputPath}");
    }

    private Option<string?> ConfigFileOption() =>
        new("--config")
        {
            Description = "Path to JSON configuration file",
            Required = false,
            Aliases = { "-cfg" },
        };

    private Option<List<string>?> DirectoriesOption() =>
        new("--directories")
        {
            Description = "List of project directories to manage (overrides config file)",
            Required = false,
            Aliases = { "-d" },
            AllowMultipleArgumentsPerToken = true,
        };

    private Option<int> ConcurrentBuildProcessesOption() =>
        new("--concurrent-build-processes")
        {
            Description = "Number of allowed concurrent build processes",
            Required = false,
            Aliases = { "-c" },
            DefaultValueFactory = (_) => ConcurrentBuildProcesses,
        };

    private Option<int> HideCursorSecondsOption() =>
        new("--hide-cursor")
        {
            Description = "Set time before the cursor is hidden, in seconds. Set to '0' to never hide.",
            Required = false,
            Aliases = { "-hr" },
            DefaultValueFactory = (_) => HideCursorSeconds,
        };

    private Option<int> MaxGitBranchLengthOption() =>
        new("--git-branch-length")
        {
            Description = "Set the maximum number of characters to be displayed for the git branch",
            Required = false,
            DefaultValueFactory = (_) => MaxGitBranchLength,
        };

    private Option<string?> BuildCommandOption() =>
        new("--build-command")
        {
            Description = "Command to use for building projects",
            Required = false,
            Aliases = { "-bc" },
        };

    private Option<string?> RunCommandOption() =>
        new("--run-command")
        {
            Description = "Command to use for running projects",
            Required = false,
            Aliases = { "-rc" },
        };

    private Option<bool> GenerateConfigOption() =>
        new("--generate-config")
        {
            Description = "Generate a sample configuration file in the current directory",
            Required = false,
            DefaultValueFactory = (_) => false,
        };

    /// <summary>
    /// Configuration file model for JSON serialization
    /// </summary>
    private class ConfigFile
    {
        public List<string>? Directories { get; set; }
        public int? ConcurrentBuildProcesses { get; set; }
        public int? MaxRetryAttempts { get; set; }
        public int? HideCursorSeconds { get; set; }
        public string? BuildCommand { get; set; }
        public string? RunCommand { get; set; }
        public bool? DumpBuildOutputToFile { get; set; }
        public int? MaxGitBranchLength { get; set; }
    }
}
