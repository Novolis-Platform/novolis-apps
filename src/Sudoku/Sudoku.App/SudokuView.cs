using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Game.Sudoku;
using Sudoku.Core;

namespace Sudoku.App;

/// <summary>
/// Compact classic Sudoku host: board + keypad stay in view on phone;
/// campaign is a linear 1–N ladder with local progress files.
/// </summary>
public sealed class SudokuView : UserControl
{
    private readonly SudokuGameService _game;
    private readonly Grid _board = new();
    private readonly Button[] _cellButtons = new Button[SudokuConstants.CellCount];
    private readonly Button[] _numberButtons = new Button[SudokuConstants.SymbolCount + 1];
    private readonly Button _clearButton = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _sideInfo = new();
    private readonly Button _notesButton = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly DispatcherTimer _timer;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private int _selectedCell = -1;
    private bool _notesMode;
    private bool _loaded;
    private bool _arrangingBody;
    private bool _narrowBody = true;
    private string _message = "Continue your ladder whenever you are ready.";
    private Grid? _bodyHost;
    private Border? _boardCard;
    private Control? _keypadStrip;
    private StackPanel? _sidePanel;
    private ScrollViewer? _sideScroll;

    /// <summary>Creates the board over a game service.</summary>
    public SudokuView(SudokuGameService game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        Focusable = true;
        _game.StateChanged += (_, _) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                Refresh();
            else
                Dispatcher.UIThread.Post(Refresh);
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastTickUtc;
            _lastTickUtc = now;
            _game.Advance(elapsed);
        };

        Content = BuildLayout();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        KeyDown += OnKeyDown;
        Refresh();
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_loaded)
            return;
        _loaded = true;
        _lastTickUtc = DateTime.UtcNow;
        _timer.Start();
        _ = Dispatcher.UIThread.InvokeAsync(BootstrapAsync);
    }

    private async Task BootstrapAsync()
    {
        try
        {
            if (await _game.LoadLatestAsync().ConfigureAwait(true))
                SetStatus("Resumed your saved puzzle.");
            else
            {
                _game.ContinueCampaign();
                SetStatus($"Puzzle {_game.CurrentLadderNumber} of {_game.Ladder.Count}.");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
            try
            {
                _game.ContinueCampaign();
            }
            catch (Exception startException)
            {
                SetStatus($"Start failed: {startException.Message}");
            }
        }
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _timer.Stop();
        _game.WriteRecovery();
        _ = _game.SaveAsync();
    }

    private Control BuildLayout()
    {
        _title.Text = "Novolis Sudoku";
        _title.FontSize = 22;
        _title.FontWeight = FontWeight.Bold;
        _title.Foreground = GraphicalProfile.TextBrush;

        _subtitle.Text = "Puzzle 1 / 69";
        _subtitle.FontSize = 13;
        _subtitle.Foreground = GraphicalProfile.MutedBrush;
        _subtitle.TextWrapping = TextWrapping.NoWrap;

        var header = new StackPanel
        {
            Margin = new Thickness(14, 10, 14, 4),
            Spacing = 2,
            Children = { _title, _subtitle },
        };

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(10, 0, 10, 6),
            Orientation = Orientation.Horizontal,
        };
        ConfigureChromeButton(_prevButton, "Prev", () =>
        {
            if (_game.TryPrevious())
                SetStatus($"Puzzle {_game.CurrentLadderNumber}.");
            else
                SetStatus("This is the first unlocked puzzle.");
        });
        ConfigureChromeButton(_nextButton, "Next", () =>
        {
            if (_game.TryNext())
                SetStatus($"Puzzle {_game.CurrentLadderNumber}.");
            else
                SetStatus("Clear this puzzle to unlock the next.");
        });
        toolbar.Children.Add(_prevButton);
        toolbar.Children.Add(_nextButton);
        toolbar.Children.Add(ChromeButton("Hint", ApplyHint));
        toolbar.Children.Add(ChromeButton("Undo", Undo));
        toolbar.Children.Add(ChromeButton("Redo", Redo));
        _notesButton.Content = "Notes";
        StyleChromeButton(_notesButton);
        _notesButton.Click += (_, _) =>
        {
            _notesMode = !_notesMode;
            _notesButton.Content = _notesMode ? "Notes ●" : "Notes";
            SetStatus(_notesMode ? "Notes on." : "Notes off.");
        };
        toolbar.Children.Add(_notesButton);

        BuildBoard();
        _boardCard = new Border
        {
            Child = _board,
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
        };

        _keypadStrip = BuildKeypadStrip();
        _sidePanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Card("CAMPAIGN", _sideInfo),
                BuildInputCardDesktop(),
            },
        };
        _sideScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        _bodyHost = new Grid { Margin = new Thickness(8, 0, 8, 4) };
        _bodyHost.SizeChanged += (_, _) => ArrangeSudokuBody();
        ArrangeSudokuBody();

        _status.Margin = new Thickness(12, 2, 12, 8);
        _status.FontSize = 12;
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.TextTrimming = TextTrimming.CharacterEllipsis;

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(toolbar);
        root.Children.Add(_status);
        root.Children.Add(_bodyHost);
        return root;
    }

    private void ArrangeSudokuBody()
    {
        if (_arrangingBody
            || _bodyHost is null
            || _boardCard is null
            || _keypadStrip is null
            || _sidePanel is null
            || _sideScroll is null)
            return;

        _arrangingBody = true;
        try
        {
            var width = _bodyHost.Bounds.Width;
            var height = _bodyHost.Bounds.Height;
            var narrow = OperatingSystem.IsAndroid() || width <= 0 || width < 720;
            if (narrow != _narrowBody || _bodyHost.Children.Count == 0)
            {
                _narrowBody = narrow;
                DetachFromParent(_boardCard);
                DetachFromParent(_keypadStrip);
                DetachFromParent(_sidePanel);
                _bodyHost.Children.Clear();
                _bodyHost.RowDefinitions.Clear();
                _bodyHost.ColumnDefinitions.Clear();
                _sideScroll.Content = null;

                if (narrow)
                {
                    // Board fills; keypad pinned under it — no scroll stack.
                    _bodyHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                    _bodyHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    _bodyHost.Children.Add(_boardCard);
                    Grid.SetRow(_keypadStrip, 1);
                    _bodyHost.Children.Add(_keypadStrip);
                }
                else
                {
                    _sideScroll.Content = _sidePanel;
                    _bodyHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    _bodyHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
                    _bodyHost.Children.Add(_boardCard);
                    Grid.SetColumn(_sideScroll, 1);
                    _sideScroll.Margin = new Thickness(12, 0, 0, 0);
                    _bodyHost.Children.Add(_sideScroll);
                }
            }

            if (width <= 0)
                width = 360;
            if (narrow)
            {
                // Keep keypad + status reachable: board eats remaining height.
                var keypadBudget = 118;
                var boardBudget = height > 0
                    ? Math.Max(180, height - keypadBudget)
                    : width - 12;
                FitBoard(Math.Min(width - 12, boardBudget));
            }
            else if (height > 0)
            {
                FitBoard(Math.Min(width - 312, height) - 12);
            }
        }
        finally
        {
            _arrangingBody = false;
        }
    }

    private void FitBoard(double available)
    {
        if (available <= 0)
            return;

        var side = Math.Clamp(available, 160, 690);
        var cell = side / SudokuConstants.Size;
        if (Math.Abs(_board.Width - side) < 0.5 && Math.Abs(_board.Height - side) < 0.5)
        {
            ApplyCellFonts();
            return;
        }

        _board.RowDefinitions.Clear();
        _board.ColumnDefinitions.Clear();
        for (var index = 0; index < SudokuConstants.Size; index++)
        {
            _board.RowDefinitions.Add(new RowDefinition(new GridLength(cell)));
            _board.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(cell)));
        }

        _board.Width = side;
        _board.Height = side;
        _board.MinWidth = side;
        _board.MaxWidth = side;
        _board.MinHeight = side;
        _board.MaxHeight = side;
        _board.HorizontalAlignment = HorizontalAlignment.Center;
        _board.VerticalAlignment = VerticalAlignment.Top;
        foreach (var button in _cellButtons)
        {
            if (button is null)
                continue;
            button.Width = cell;
            button.Height = cell;
            button.MinWidth = 0;
            button.MaxWidth = cell;
            button.MinHeight = 0;
            button.MaxHeight = cell;
            button.Padding = new Thickness(0);
        }

        ApplyCellFonts();
    }

    private static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl content:
                if (ReferenceEquals(content.Content, control))
                    content.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
        }
    }

    private void ApplyCellFonts()
    {
        var side = _board.Width > 0 ? _board.Width : 360;
        var cell = side / SudokuConstants.Size;
        var font = Math.Clamp(cell * 0.42, 11, 28);
        foreach (var button in _cellButtons)
        {
            if (button is null)
                continue;
            button.MinWidth = 0;
            button.MinHeight = 0;
            var notes = button.Content is string text && text.Contains(' ', StringComparison.Ordinal);
            button.FontSize = notes ? Math.Max(8, font * 0.42) : font;
        }
    }

    private void BuildBoard()
    {
        for (var index = 0; index < SudokuConstants.Size; index++)
        {
            _board.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _board.Width = 320;
        _board.Height = 320;
        _board.HorizontalAlignment = HorizontalAlignment.Center;
        _board.VerticalAlignment = VerticalAlignment.Center;
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            var cell = SudokuCell.FromIndex(cellIndex);
            var button = new Button
            {
                Tag = cellIndex,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = GraphicalProfile.BorderBrush,
                BorderThickness = new Thickness(
                    cell.Column % SudokuConstants.BoxSize == 0 ? 2 : 1,
                    cell.Row % SudokuConstants.BoxSize == 0 ? 2 : 1,
                    (cell.Column + 1) % SudokuConstants.BoxSize == 0 ? 2 : 1,
                    (cell.Row + 1) % SudokuConstants.BoxSize == 0 ? 2 : 1),
                CornerRadius = new CornerRadius(0),
            };
            var selectedIndex = cellIndex;
            button.Click += (_, _) =>
            {
                _selectedCell = selectedIndex;
                Focus();
                Refresh();
            };
            _cellButtons[cellIndex] = button;
            Grid.SetRow(button, cell.Row);
            Grid.SetColumn(button, cell.Column);
            _board.Children.Add(button);
        }
    }

    private Control BuildKeypadStrip()
    {
        var row = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*,*,*,*,*,*,Auto"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var number = value;
            var button = new Button
            {
                Content = number.ToString(),
                MinHeight = 44,
                MinWidth = 0,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = GraphicalProfile.RaisedBrush,
                Foreground = GraphicalProfile.TextBrush,
                FontWeight = FontWeight.SemiBold,
            };
            button.Click += (_, _) => ApplyValue(number);
            _numberButtons[number] = button;
            Grid.SetColumn(button, number - 1);
            row.Children.Add(button);
        }

        _clearButton.Content = "⌫";
        _clearButton.MinHeight = 44;
        _clearButton.MinWidth = 44;
        _clearButton.Margin = new Thickness(2, 0, 0, 0);
        _clearButton.Padding = new Thickness(0);
        _clearButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _clearButton.VerticalContentAlignment = VerticalAlignment.Center;
        _clearButton.Background = GraphicalProfile.RaisedBrush;
        _clearButton.Foreground = GraphicalProfile.TextBrush;
        _clearButton.Click += (_, _) => ApplyValue(SudokuConstants.Empty);
        Grid.SetColumn(_clearButton, 9);
        row.Children.Add(_clearButton);

        return new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(6),
            Child = row,
        };
    }

    private Control BuildInputCardDesktop()
    {
        // Desktop keeps a 3×3 pad; phone uses the compact strip above.
        var keypad = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
        };
        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var number = value;
            var button = new Button
            {
                Content = number.ToString(),
                MinHeight = 44,
                Margin = new Thickness(0, 0, 6, 6),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = GraphicalProfile.RaisedBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            button.Click += (_, _) => ApplyValue(number);
            // Phone strip owns _numberButtons; desktop mirrors without shared refs.
            Grid.SetRow(button, (number - 1) / 3);
            Grid.SetColumn(button, (number - 1) % 3);
            keypad.Children.Add(button);
        }

        var clear = new Button
        {
            Content = "Clear",
            MinHeight = 44,
            Margin = new Thickness(0, 0, 6, 6),
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        clear.Click += (_, _) => ApplyValue(SudokuConstants.Empty);
        Grid.SetRow(clear, 3);
        Grid.SetColumnSpan(clear, 3);
        keypad.Children.Add(clear);

        return Card("INPUT", keypad);
    }

    private static Border Card(string title, Control content)
    {
        var heading = new TextBlock
        {
            Text = title,
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
        };
        return new Border
        {
            Padding = new Thickness(12),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children = { heading, content },
            },
        };
    }

    private static Button ChromeButton(string label, Action action)
    {
        var button = new Button { Content = label };
        StyleChromeButton(button);
        button.Click += (_, _) => action();
        return button;
    }

    private static void ConfigureChromeButton(Button button, string label, Action action)
    {
        button.Content = label;
        StyleChromeButton(button);
        button.Click += (_, _) => action();
    }

    private static void StyleChromeButton(Button button)
    {
        button.MinHeight = 36;
        button.Margin = new Thickness(0, 0, 6, 4);
        button.Padding = new Thickness(10, 4);
        button.Background = GraphicalProfile.AccentFillBrush;
        button.Foreground = GraphicalProfile.OnAccentFillBrush;
    }

    private void Refresh()
    {
        var session = _game.Current;
        var progress = _game.Progress;
        var ladder = _game.Ladder;
        var step = _game.CurrentLadderStep;

        if (session is null || step is null)
        {
            _subtitle.Text = "No puzzle loaded";
            _sideInfo.Text = "Start the campaign.";
            _status.Text = _message;
            return;
        }

        var conquered = progress.ConqueredPuzzleIds.Count;
        _subtitle.Text =
            $"#{step.Number}/{ladder.Count} · {step.SegmentDisplayName}"
            + $" · {FormatElapsed(session.Elapsed)}"
            + $" · {session.Mistakes}✗ · {session.HintsUsed}?"
            + $" · cleared {conquered}/{ladder.Count}";

        _sideInfo.Text =
            $"Ladder #{step.Number} of {ladder.Count}"
            + Environment.NewLine
            + $"{step.SegmentDisplayName} · id {step.PuzzleId}"
            + Environment.NewLine
            + $"{SudokuHelpers.CountFilled(session.Entries)}/{SudokuConstants.CellCount} filled"
            + Environment.NewLine
            + $"Unlocked through #{progress.HighestUnlockedNumber}"
            + Environment.NewLine
            + $"Completions {progress.Completions} · play {FormatElapsed(TimeSpan.FromSeconds(progress.TotalPlaySeconds))}";

        _prevButton.IsEnabled = step.Number > 1;
        _nextButton.IsEnabled = step.Number < progress.HighestUnlockedNumber;

        if (session.IsCompleted)
            _message = step.Number < ladder.Count
                ? "Cleared — Next unlocks the following puzzle."
                : "Campaign complete. You can replay any puzzle.";

        var conflicts = _selectedCell >= 0
            ? SudokuHelpers.FindConflicts(session.Entries, _selectedCell).ToHashSet()
            : [];
        var selectedValue = _selectedCell >= 0
            ? session.Entries.Get(_selectedCell)
            : SudokuConstants.Empty;
        var valueCounts = new int[SudokuConstants.SymbolCount + 1];
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            var value = session.Entries.Get(cellIndex);
            if (value != SudokuConstants.Empty)
                valueCounts[value]++;
        }

        for (var cellIndex = 0; cellIndex < _cellButtons.Length; cellIndex++)
        {
            var button = _cellButtons[cellIndex];
            var cell = SudokuCell.FromIndex(cellIndex);
            var value = session.Entries.Get(cellIndex);
            var isGiven = session.IsGiven(cellIndex);
            var isWrong = !isGiven
                          && value != SudokuConstants.Empty
                          && value != session.Solution.Get(cellIndex);
            var isRelated = _selectedCell >= 0
                            && (cell.Row == SudokuConstants.RowOf(_selectedCell)
                                || cell.Column == SudokuConstants.ColumnOf(_selectedCell)
                                || cell.Box == SudokuConstants.BoxOf(_selectedCell));
            var isSelected = cellIndex == _selectedCell;
            var isValueMatch = selectedValue != SudokuConstants.Empty
                               && value == selectedValue;
            button.Background = isSelected
                ? GraphicalProfile.AccentFillBrush
                : isWrong || conflicts.Contains(cellIndex)
                    ? GraphicalProfile.DangerBrush
                    : isValueMatch
                        ? GraphicalProfile.ActionSoftBrush
                        : isRelated
                            ? GraphicalProfile.RaisedBrush
                        : isGiven
                            ? GraphicalProfile.RaisedBrush
                            : GraphicalProfile.SurfaceBrush;
            button.Foreground = isSelected
                ? GraphicalProfile.OnAccentFillBrush
                : isWrong || conflicts.Contains(cellIndex)
                    ? GraphicalProfile.TextBrush
                    : isGiven
                        ? GraphicalProfile.AccentBrush
                        : GraphicalProfile.TextBrush;
            button.FontWeight = isGiven ? FontWeight.Bold : FontWeight.Normal;
            button.Content = value == SudokuConstants.Empty
                ? string.Join(" ", session.GetNotes(cellIndex).Values())
                : value.ToString();
        }

        ApplyCellFonts();

        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var isSelectedValue = value == selectedValue;
            var isComplete = valueCounts[value] >= SudokuConstants.Size;
            var button = _numberButtons[value];
            button.IsEnabled = !isComplete;
            button.Opacity = isComplete ? 0.42 : 1;
            button.Background = isComplete
                ? GraphicalProfile.SurfaceBrush
                : isSelectedValue
                    ? GraphicalProfile.AccentFillBrush
                    : GraphicalProfile.RaisedBrush;
            button.Foreground = isComplete
                ? GraphicalProfile.MutedBrush
                : isSelectedValue
                    ? GraphicalProfile.OnAccentFillBrush
                    : GraphicalProfile.TextBrush;
        }
        _clearButton.Background = GraphicalProfile.RaisedBrush;
        _clearButton.Foreground = GraphicalProfile.TextBrush;

        _status.Text = _game.LastStorageError is null
            ? _message
            : $"{_message} · {_game.LastStorageError}";
    }

    private void ApplyHint()
    {
        var hint = _game.ApplyHint();
        if (hint is null)
        {
            SetStatus("No simple single available.");
            return;
        }

        _selectedCell = hint.CellIndex;
        SetStatus($"Hint: {hint.Reason}");
    }

    private void Undo() =>
        SetStatus(_game.Undo() ? "Undo." : "Nothing to undo.");

    private void Redo() =>
        SetStatus(_game.Redo() ? "Redo." : "Nothing to redo.");

    private void ApplyValue(byte value)
    {
        if (_selectedCell < 0)
        {
            SetStatus("Select a cell first.");
            return;
        }

        if (_notesMode && value != SudokuConstants.Empty)
        {
            SetStatus(_game.ToggleNote(_selectedCell, value)
                ? $"Note {value}."
                : "Notes need an empty cell.");
            return;
        }

        _game.TrySetValue(_selectedCell, value, out var message);
        SetStatus(message);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (TryGetDigit(args.Key, out var value))
        {
            ApplyValue(value);
            args.Handled = true;
            return;
        }

        if (args.Key is Key.Delete or Key.Back)
        {
            ApplyValue(SudokuConstants.Empty);
            args.Handled = true;
            return;
        }

        if (args.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelection(args.Key);
            args.Handled = true;
        }
    }

    private void MoveSelection(Key key)
    {
        if (_selectedCell < 0)
        {
            _selectedCell = 0;
            Refresh();
            return;
        }

        var row = SudokuConstants.RowOf(_selectedCell);
        var column = SudokuConstants.ColumnOf(_selectedCell);
        switch (key)
        {
            case Key.Left:
                column = (column + SudokuConstants.Size - 1) % SudokuConstants.Size;
                break;
            case Key.Right:
                column = (column + 1) % SudokuConstants.Size;
                break;
            case Key.Up:
                row = (row + SudokuConstants.Size - 1) % SudokuConstants.Size;
                break;
            case Key.Down:
                row = (row + 1) % SudokuConstants.Size;
                break;
        }

        _selectedCell = SudokuConstants.CellIndex(row, column);
        Refresh();
    }

    private void SetStatus(string message)
    {
        _message = message;
        Refresh();
    }

    private static bool TryGetDigit(Key key, out byte value)
    {
        value = key switch
        {
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            Key.D6 or Key.NumPad6 => 6,
            Key.D7 or Key.NumPad7 => 7,
            Key.D8 or Key.NumPad8 => 8,
            Key.D9 or Key.NumPad9 => 9,
            _ => 0,
        };
        return value != 0;
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss")
            : elapsed.ToString(@"m\:ss");
}
