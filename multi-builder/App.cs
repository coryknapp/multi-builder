using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        
        // REQUIRED: DataGrid theme - without this, DataGrid won't render
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = Program.Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(viewModel);

            var buildService = Program.Services.GetRequiredService<BuildService>();
            _ = buildService.StartBuildQueueProcessing();

            // Kill all child processes when the application exits
            desktop.ShutdownRequested += OnShutdownRequested;
            desktop.Exit += OnExit;
        }

        // Also handle unexpected termination via AppDomain
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        KillAllChildProcesses();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        KillAllChildProcesses();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        KillAllChildProcesses();
    }

    private void KillAllChildProcesses()
    {
        try
        {
            var killService = Program.Services.GetRequiredService<KillService>();
            killService.KillAllProjects();
        }
        catch
        {
            // Service provider may not be available during shutdown
        }
    }
}