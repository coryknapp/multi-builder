using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Data;
using System;
using Avalonia.Styling;

public class LogViewerWindow : Window
{
    private static readonly Color EvenRowColor = Color.FromRgb(30, 30, 30);
    private static readonly Color OddRowColor = Color.FromRgb(45, 45, 45);
    private static readonly Color ErrorColor = Color.FromRgb(80, 30, 30);
    private static readonly Color SearchMatchColor = Color.FromRgb(60, 60, 20);

    private readonly int rowSpacing = 4; // Controls distance between rows

    private LogViewerViewModel viewModel;
    private ListBox logListBox;
    private TextBox searchBox;
    private bool autoScroll = true;

    public LogViewerWindow(LogViewerViewModel viewModel)
    {
        this.viewModel = viewModel;

        Title = viewModel.Title;
        Width = 1000;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var toolbar = CreateToolbar();
        var statusBar = CreateStatusBar();
        logListBox = CreateLogListBox();

        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);

        Content = new DockPanel
        {
            LastChildFill = true,
            Children = { toolbar, statusBar, logListBox }
        };

        // Auto-scroll when new items added
        viewModel.FilteredLines.CollectionChanged += (_, _) =>
        {
            if (autoScroll && logListBox.ItemCount > 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    logListBox.ScrollIntoView(logListBox.ItemCount - 1);
                });
            }
        };

        Closed += (_, _) => viewModel.Stop();
        KeyDown += OnKeyDown;
    }

    private Control CreateToolbar()
    {
        searchBox = new TextBox
        {
            Watermark = "Search...",
            Width = 200,
            Margin = new Thickness(5)
        };
        searchBox.TextChanged += (_, _) => viewModel.SearchText = searchBox.Text ?? "";

        var clearSearchBtn = new Button
        {
            Content = "✕",
            Padding = new Thickness(8, 5),
            Margin = new Thickness(0, 5, 10, 5)
        };
        clearSearchBtn.Click += (_, _) =>
        {
            searchBox.Text = "";
            viewModel.ClearSearch();
        };

        var pauseBtn = new Button
        {
            Padding = new Thickness(10, 5),
            Margin = new Thickness(5)
        };
        pauseBtn.Bind(Button.ContentProperty, new Binding("PauseButtonText") { Source = viewModel });
        pauseBtn.Click += (_, _) => viewModel.TogglePause();

        var autoScrollCheckBox = new CheckBox
        {
            Content = "Auto-scroll",
            IsChecked = true,
            Margin = new Thickness(10, 5),
            VerticalAlignment = VerticalAlignment.Center
        };
        autoScrollCheckBox.IsCheckedChanged += (_, _) => autoScroll = autoScrollCheckBox.IsChecked ?? true;

        var scrollToEndBtn = new Button
        {
            Content = "⬇ Scroll to End",
            Padding = new Thickness(10, 5),
            Margin = new Thickness(5)
        };
        scrollToEndBtn.Click += (_, _) =>
        {
            if (logListBox.ItemCount > 0)
                logListBox.ScrollIntoView(logListBox.ItemCount - 1);
        };

        var scrollToTopBtn = new Button
        {
            Content = "⬆ Scroll to Top",
            Padding = new Thickness(10, 5),
            Margin = new Thickness(5)
        };
        scrollToTopBtn.Click += (_, _) =>
        {
            if (logListBox.ItemCount > 0)
                logListBox.ScrollIntoView(0);
        };

        return new Border
        {
            //Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = "🔍",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 0, 5, 0)
                    },
                    searchBox,
                    clearSearchBtn,
                    new Border { Width = 1, Background = Brushes.Gray, Margin = new Thickness(5, 8) },
                    pauseBtn,
                    autoScrollCheckBox,
                    new Border { Width = 1, Background = Brushes.Gray, Margin = new Thickness(5, 8) },
                    scrollToTopBtn,
                    scrollToEndBtn,
                }
            }
        };
    }

    private ListBox CreateLogListBox()
    {
        var listBox = new ListBox
        {
            ItemsSource = viewModel.FilteredLines,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Margin = new Thickness(0),
            Padding = new Thickness(0)
        };

        listBox.ItemContainerTheme = new ControlTheme(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
                new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent),
            }
        };

        int halfSpacing = rowSpacing / 2;

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LogLineViewModel>((item, _) =>
        {
            if (item == null)
            {
                return new Border { Height = 0 };
            }

            var background = item.IsMatch ? SearchMatchColor
                           : item.IsError ? ErrorColor
                           : item.IsEvenRow ? EvenRowColor
                           : OddRowColor;

            var foreground = item.IsError ? Colors.Gold: Colors.White;

            // Use Grid for proper text wrapping support
            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*")
            };

            var timestampBlock = new TextBlock
            {
                Text = item.Timestamp,
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Width = 90,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(timestampBlock, 0);

            var contentBlock = new TextBlock
            {
                Text = item.Content.Trim(),
                Foreground = new SolidColorBrush(foreground),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(contentBlock, 1);

            contentGrid.Children.Add(timestampBlock);
            contentGrid.Children.Add(contentBlock);

            // Split spacing: half above (margin), half below (padding extends background)
            return new Border
            {
                Background = new SolidColorBrush(background),
                Padding = new Thickness(5, 2 + halfSpacing, 5, 2 + halfSpacing),
                Margin = new Thickness(0, halfSpacing, 0, 0),
                Child = contentGrid
            };
        });

        return listBox;
    }

    private Control CreateStatusBar()
    {
        var lineCountText = new TextBlock
        {
            Foreground = Brushes.Gray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Update line count display
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(viewModel.TotalLineCount) or nameof(viewModel.FilteredLineCount))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    lineCountText.Text = string.IsNullOrEmpty(viewModel.SearchText)
                        ? $"{viewModel.TotalLineCount} lines"
                        : $"{viewModel.FilteredLineCount} / {viewModel.TotalLineCount} lines (filtered)";
                });
            }
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Padding = new Thickness(10, 5),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    lineCountText,
                    new TextBlock
                    {
                        Text = " | Ctrl+F: Search | Esc: Close",
                        Foreground = Brushes.DarkGray,
                        FontSize = 12,
                        Margin = new Thickness(20, 0, 0, 0)
                    }
                }
            }
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            searchBox.Focus();
            searchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            viewModel.TogglePause();
            e.Handled = true;
        }
    }

    public static void Show(ManagedProject project, LogCollectorService logCollectorService, LogViewType logType)
    {
        var viewModel = new LogViewerViewModel(project, logCollectorService, logType);
        var window = new LogViewerWindow(viewModel);
        window.Show();
    }
}