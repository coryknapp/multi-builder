using Avalonia;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = ConfigureServices(args);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static IServiceProvider ConfigureServices(string[] args)
    {
        var services = new ServiceCollection();

        // Parse options first
        var optionService = new OptionService();
        optionService.ParseOptions(args);

        services.AddSingleton(optionService);
        services.AddSingleton<LogCollectorService>();
        services.AddSingleton<LogFileService>();
        services.AddSingleton<BuildErrorParserService>();
        services.AddSingleton<KillService>();
        services.AddSingleton<GitService>();
        services.AddSingleton<RunService>();
        services.AddSingleton<BuildService>();
        services.AddSingleton<BuildRunService>();

        // Create managed projects
        var managedProjects = optionService.Directories
            .Select(d => new ManagedProject(Path.GetFileName(d), d))
            .ToList();
        services.AddSingleton<IList<ManagedProject>>(managedProjects);

        // ViewModels
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
