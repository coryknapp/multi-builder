using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

public class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly DataGrid projectGrid;

    public MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;

        this.Title = "Multi-Builder";
        this.Width = 1100;
        this.Height = 600;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var toolbar = this.CreateToolbar();
        var statusBar = this.CreateStatusBar();
        projectGrid = this.CreateProjectGrid();

        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);

        var dockPanel = new DockPanel
        {
            LastChildFill = true
        };
        
        dockPanel.Children.Add(toolbar);
        dockPanel.Children.Add(statusBar);
        dockPanel.Children.Add(projectGrid);

        this.Content = dockPanel;

        // Use AddHandler with RoutingStrategies.Tunnel to capture keys before controls consume them
        this.AddHandler(KeyDownEvent, this.OnKeyDown, RoutingStrategies.Tunnel);
    }

    private Control CreateToolbar()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(10),
            Background = Brushes.Transparent,
            Children =
            {
                this.CreateButton("Build (B)", viewModel.BuildSelected),
                this.CreateButton("Run (R)", viewModel.RunSelected),
                this.CreateButton("Build+Run (P)", viewModel.BuildAndRunSelected),
                this.CreateButton("Kill (K)", viewModel.KillSelected),
                this.CreateToolBarTextElement()
            }
        };
    }

    private Button CreateButton(string text, Action action, string? tooltip = null)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(10, 5),
            Margin = new Thickness(2)
        };
        btn.Click += (_, _) => action();
        if (tooltip != null)
            ToolTip.SetTip(btn, tooltip);
        return btn;
    }

    private TextBlock CreateToolBarTextElement()
    {
        return new TextBlock()
        {
            Text = "(Shift for all, Alt-Shift for all except selected)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 5, 0)
        };
    }

    private DataGrid CreateProjectGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            ItemsSource = viewModel.Projects,
            Margin = new Thickness(10, 0, 10, 10),
            MinHeight = 100 // Ensure minimum height
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Project",
            Binding = new Avalonia.Data.Binding("Name"),
            Width = new DataGridLength(200)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Status",
            Binding = new Avalonia.Data.Binding("Status"),
            Width = new DataGridLength(120)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Errors",
            Binding = new Avalonia.Data.Binding("ErrorCount"),
            Width = new DataGridLength(60)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Last Build",
            Binding = new Avalonia.Data.Binding("LastBuild"),
            Width = new DataGridLength(100)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Git Branch",
            Binding = new Avalonia.Data.Binding("GitBranch"),
            Width = new DataGridLength(150)
        });

        // Add Logs column with buttons
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Logs",
            Width = new DataGridLength(160),
            CellTemplate = new FuncDataTemplate<ProjectViewModel>((project, _) =>
            {
                var buildLogsBtn = new Button
                {
                    Content = "📋 Build",
                    Padding = new Thickness(5, 2),
                    Margin = new Thickness(2, 0),
                    FontSize = 11
                };
                buildLogsBtn.Click += (_, _) => viewModel.ShowBuildLogs(project);

                var runLogsBtn = new Button
                {
                    Content = "📋 Run",
                    Padding = new Thickness(5, 2),
                    Margin = new Thickness(2, 0),
                    FontSize = 11
                };
                runLogsBtn.Click += (_, _) => viewModel.ShowRunLogs(project);

                return new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { buildLogsBtn, runLogsBtn }
                };
            })
        });

        grid.SelectionChanged += (_, _) =>
        {
            viewModel.SelectedProject = grid.SelectedItem as ProjectViewModel;
        };

        // Prevent DataGrid from handling our hotkeys
        grid.KeyDown += (s, e) =>
        {
            // Let these keys bubble up to the Window handler
            if (e.Key == Key.B || e.Key == Key.R || e.Key == Key.P || 
                e.Key == Key.K || e.Key == Key.L || e.Key == Key.O ||
                e.Key == Key.Q || e.Key == Key.Escape)
            {
                // Don't mark as handled, let it bubble up
                return;
            }
        };

        return grid;
    }

    private Control CreateStatusBar()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Padding = new Thickness(10, 5),
            MinHeight = 30, // Ensure minimum height
            Child = new TextBlock
            {
                Text = "B: Build | R: Run | P: Build+Run | K: Kill | L: Build Logs | O: Run Logs | Shift+Key: All projects | Q: Quit",
                Foreground = Brushes.Gray,
                FontSize = 12
            }
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Skip if key is being used in a text input
        if (e.Source is TextBox)
            return;

        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        switch (e.Key)
        {
            case Key.B:
                if (shift) viewModel.BuildAll();
                else viewModel.BuildSelected();
                e.Handled = true;
                break;

            case Key.R:
                if (shift) viewModel.RunAll();
                else viewModel.RunSelected();
                e.Handled = true;
                break;

            case Key.P:
                if (shift) 
                {
                    // Shift+P: Build and run all
                    viewModel.BuildAll();
                    viewModel.RunAll();
                }
                else 
                {
                    viewModel.BuildAndRunSelected();
                }
                e.Handled = true;
                break;

            case Key.K:
                if (shift) viewModel.KillAll();
                else viewModel.KillSelected();
                e.Handled = true;
                break;

            case Key.L:
                viewModel.ShowBuildLogsSelected();
                e.Handled = true;
                break;

            case Key.O:
                viewModel.ShowRunLogsSelected();
                e.Handled = true;
                break;

            case Key.Q:
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Up:
                SelectPrevious();
                e.Handled = true;
                break;

            case Key.Down:
                SelectNext();
                e.Handled = true;
                break;
        }
    }

    private void SelectNext()
    {
        var idx = projectGrid.SelectedIndex;
        if (idx < viewModel.Projects.Count - 1)
            projectGrid.SelectedIndex = idx + 1;
    }

    private void SelectPrevious()
    {
        var idx = projectGrid.SelectedIndex;
        if (idx > 0)
            projectGrid.SelectedIndex = idx - 1;
    }
}

// Helper extension
public static class ControlExtensions
{
    public static T Also<T>(this T control, Action<T, Dock> setter, Dock value) where T : Control
    {
        setter(control, value);
        return control;
    }
}