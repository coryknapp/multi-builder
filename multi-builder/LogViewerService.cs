using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Spectre.Console;

public class LogViewerService
{
    private List<string> _lines = [];
    private int _scrollOffset = 0;
    private int _selectedLineIndex = 0;
    private string _searchTerm = string.Empty;
    private List<int> _searchMatchIndices = [];
    private int _currentSearchMatchIndex = -1;
    private bool _isRunning = false;

    public void ShowLogViewer(string title, IEnumerable<string> lines)
    {
        _lines = lines?.ToList() ?? [];
        _scrollOffset = 0;
        _selectedLineIndex = 0;
        _searchTerm = string.Empty;
        _searchMatchIndices = [];
        _currentSearchMatchIndex = -1;
        _isRunning = true;

        Console.CursorVisible = false;
        Console.Clear();

        while (_isRunning)
        {
            RenderView(title);
            HandleInput();
        }

        Console.CursorVisible = true;
        Console.Clear();
    }

    public void ShowLogViewer(string title, string content)
    {
        var lines = content?.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None) ?? [];
        this.ShowLogViewer(title, lines);
    }

    private void RenderView(string title)
    {
        Console.SetCursorPosition(0, 0);

        int consoleWidth = Console.WindowWidth;
        int consoleHeight = Console.WindowHeight;
        int headerHeight = 3;
        int footerHeight = 2;
        int viewableLines = consoleHeight - headerHeight - footerHeight;

        // Header
        var titleMarkup = $"[bold cyan]??? {Markup.Escape(title)} ???[/]";
        AnsiConsole.MarkupLine(titleMarkup.PadRight(consoleWidth));

        var searchInfo = string.IsNullOrEmpty(_searchTerm)
            ? "[dim]Press '/' to search[/]"
            : $"[yellow]Search: {Markup.Escape(_searchTerm)}[/] [dim]({_searchMatchIndices.Count} matches)[/]";
        AnsiConsole.MarkupLine(searchInfo.PadRight(consoleWidth));

        var positionInfo = $"[dim]Line {_selectedLineIndex + 1}/{_lines.Count}[/]";
        AnsiConsole.MarkupLine(positionInfo.PadRight(consoleWidth));

        // Content area
        EnsureSelectedLineVisible(viewableLines);

        for (int i = 0; i < viewableLines; i++)
        {
            int lineIndex = _scrollOffset + i;

            if (lineIndex < _lines.Count)
            {
                RenderLine(lineIndex, consoleWidth);
            }
            else
            {
                Console.WriteLine(new string(' ', consoleWidth));
            }
        }

        // Footer
        RenderFooter(consoleWidth);
    }

    private void RenderLine(int lineIndex, int consoleWidth)
    {
        string line = _lines[lineIndex];
        bool isSelected = lineIndex == _selectedLineIndex;
        bool isSearchMatch = _searchMatchIndices.Contains(lineIndex);
        bool isCurrentSearchMatch = _currentSearchMatchIndex >= 0 &&
                                    _currentSearchMatchIndex < _searchMatchIndices.Count &&
                                    _searchMatchIndices[_currentSearchMatchIndex] == lineIndex;

        // Determine display line - full line if selected, truncated otherwise
        string displayLine = isSelected ? line : TruncateLine(line, consoleWidth - 4);

        // Build the markup
        var markup = new StringBuilder();

        if (isSelected)
        {
            markup.Append("[black on white]");
        }
        else if (isCurrentSearchMatch)
        {
            markup.Append("[black on yellow]");
        }
        else if (isSearchMatch)
        {
            markup.Append("[yellow]");
        }

        // Line number prefix
        string lineNum = $"{lineIndex + 1,4}: ";

        if (!string.IsNullOrEmpty(_searchTerm) && !isSelected)
        {
            // Highlight search terms within the line
            markup.Append(Markup.Escape(lineNum));
            markup.Append(HighlightSearchTerm(displayLine, _searchTerm, isSelected || isCurrentSearchMatch));
        }
        else
        {
            markup.Append(Markup.Escape(lineNum + displayLine));
        }

        if (isSelected || isCurrentSearchMatch || isSearchMatch)
        {
            markup.Append("[/]");
        }

        // Pad or truncate to fit console width
        string finalOutput = markup.ToString();

        try
        {
            AnsiConsole.Markup(finalOutput);
            // Clear rest of line
            int markuplessLength = (lineNum + displayLine).Length;
            int padding = Math.Max(0, consoleWidth - markuplessLength);
            Console.WriteLine(new string(' ', padding));
        }
        catch
        {
            // Fallback if markup fails
            Console.WriteLine(TruncateLine(line, consoleWidth));
        }
    }

    private string HighlightSearchTerm(string line, string searchTerm, bool skipHighlight)
    {
        if (skipHighlight || string.IsNullOrEmpty(searchTerm))
        {
            return Markup.Escape(line);
        }

        var result = new StringBuilder();
        int lastIndex = 0;
        int index;

        while ((index = line.IndexOf(searchTerm, lastIndex, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            // Add text before the match
            if (index > lastIndex)
            {
                result.Append(Markup.Escape(line[lastIndex..index]));
            }

            // Add highlighted match
            result.Append("[bold red on yellow]");
            result.Append(Markup.Escape(line.Substring(index, searchTerm.Length)));
            result.Append("[/]");

            lastIndex = index + searchTerm.Length;
        }

        // Add remaining text
        if (lastIndex < line.Length)
        {
            result.Append(Markup.Escape(line[lastIndex..]));
        }

        return result.ToString();
    }

    private static string TruncateLine(string line, int maxWidth)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        if (line.Length <= maxWidth)
        {
            return line;
        }

        return maxWidth > 3 ? line[..(maxWidth - 3)] + "..." : line[..maxWidth];
    }

    private void RenderFooter(int consoleWidth)
    {
        var controls = "[dim]??[/] Navigate  [dim]PgUp/PgDn[/] Scroll  [dim]/[/] Search  [dim]n/N[/] Next/Prev Match  [dim]Home/End[/] Jump  [dim]Esc/q[/] Exit";
        AnsiConsole.MarkupLine(controls);

        var scrollIndicator = _lines.Count > 0
            ? $"[dim]Scroll: {(_scrollOffset * 100 / Math.Max(1, _lines.Count)):D}%[/]"
            : "[dim]Empty[/]";
        AnsiConsole.MarkupLine(scrollIndicator.PadRight(consoleWidth));
    }

    private void EnsureSelectedLineVisible(int viewableLines)
    {
        if (_selectedLineIndex < _scrollOffset)
        {
            _scrollOffset = _selectedLineIndex;
        }
        else if (_selectedLineIndex >= _scrollOffset + viewableLines)
        {
            _scrollOffset = _selectedLineIndex - viewableLines + 1;
        }

        // Clamp scroll offset
        _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, Math.Max(0, _lines.Count - viewableLines)));
    }

    private void HandleInput()
    {
        if (!Console.KeyAvailable)
        {
            System.Threading.Thread.Sleep(16); // ~60fps refresh
            return;
        }

        var key = Console.ReadKey(intercept: true);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                break;

            case ConsoleKey.DownArrow:
                MoveSelection(1);
                break;

            case ConsoleKey.PageUp:
                MoveSelection(-GetPageSize());
                break;

            case ConsoleKey.PageDown:
                MoveSelection(GetPageSize());
                break;

            case ConsoleKey.Home:
                _selectedLineIndex = 0;
                _scrollOffset = 0;
                break;

            case ConsoleKey.End:
                _selectedLineIndex = Math.Max(0, _lines.Count - 1);
                break;

            case ConsoleKey.Escape:
                _isRunning = false;
                break;

            case ConsoleKey.Q:
                if (!IsInSearchMode())
                {
                    _isRunning = false;
                }
                break;

            case ConsoleKey.N:
                if (key.Modifiers == ConsoleModifiers.Shift)
                {
                    NavigateSearchMatch(-1);
                }
                else
                {
                    NavigateSearchMatch(1);
                }
                break;

            case ConsoleKey.Oem2 when key.KeyChar == '/':
            case ConsoleKey.Divide:
                EnterSearchMode();
                break;

            case ConsoleKey.F3:
                if (key.Modifiers == ConsoleModifiers.Shift)
                {
                    NavigateSearchMatch(-1);
                }
                else
                {
                    NavigateSearchMatch(1);
                }
                break;
        }
    }

    private bool IsInSearchMode() => false; // Search mode is modal via EnterSearchMode

    private void MoveSelection(int delta)
    {
        _selectedLineIndex = Math.Clamp(_selectedLineIndex + delta, 0, Math.Max(0, _lines.Count - 1));
    }

    private int GetPageSize()
    {
        return Math.Max(1, Console.WindowHeight - 5);
    }

    private void EnterSearchMode()
    {
        Console.CursorVisible = true;
        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        AnsiConsole.Markup("[yellow]Search: [/]");

        var searchBuilder = new StringBuilder(_searchTerm);
        Console.Write(_searchTerm);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                _searchTerm = searchBuilder.ToString();
                PerformSearch();
                break;
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                break;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (searchBuilder.Length > 0)
                {
                    searchBuilder.Remove(searchBuilder.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                searchBuilder.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }

        Console.CursorVisible = false;
    }

    private void PerformSearch()
    {
        _searchMatchIndices.Clear();
        _currentSearchMatchIndex = -1;

        if (string.IsNullOrEmpty(_searchTerm))
        {
            return;
        }

        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                _searchMatchIndices.Add(i);
            }
        }

        // Jump to first match at or after current position
        if (_searchMatchIndices.Count > 0)
        {
            _currentSearchMatchIndex = _searchMatchIndices.FindIndex(idx => idx >= _selectedLineIndex);
            if (_currentSearchMatchIndex == -1)
            {
                _currentSearchMatchIndex = 0;
            }
            _selectedLineIndex = _searchMatchIndices[_currentSearchMatchIndex];
        }
    }

    private void NavigateSearchMatch(int direction)
    {
        if (_searchMatchIndices.Count == 0)
        {
            return;
        }

        _currentSearchMatchIndex += direction;

        if (_currentSearchMatchIndex >= _searchMatchIndices.Count)
        {
            _currentSearchMatchIndex = 0;
        }
        else if (_currentSearchMatchIndex < 0)
        {
            _currentSearchMatchIndex = _searchMatchIndices.Count - 1;
        }

        _selectedLineIndex = _searchMatchIndices[_currentSearchMatchIndex];
    }

    public void ClearSearch()
    {
        _searchTerm = string.Empty;
        _searchMatchIndices.Clear();
        _currentSearchMatchIndex = -1;
    }
}
