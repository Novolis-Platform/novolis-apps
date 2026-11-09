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

/// <summary>Code-only interactive classic Sudoku board and command surface.</summary>
public sealed class SudokuView : UserControl
{
    private readonly SudokuGameService _game;
    private readonly Grid _board = new();
    private readonly Button[] _cellButtons = new Button[SudokuConstants.CellCount];
    private readonly Button[] _numberButtons = new Button[SudokuConstants.SymbolCount + 1];
    private readonly Button _clearButton = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _progress = new();
    private readonly TextBlock _gameInfo = new();
    private readonly Button _notesButton = new();
    private readonly DispatcherTimer _timer;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private int _selectedCell = -1;
    private bool _notesMode;
    private bool _loaded;
    private bool _arrangingBody;
    private bool _narrowBody = true;
    private string _message = "Choose a difficulty to begin.";
    private Grid? _bodyHost;
    private Border? _boardCard;
    private StackPanel? _sidePanel;
    private ScrollViewer? _narrowScroll;
    private ScrollViewer? _sideScroll;

    /// <summary>Creates the board over a game service.</summary>
    public SudokuView(SudokuGameService game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        Focusable = true;
        _game.StateChanged += (_, _) => Refresh();
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

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_loaded)
            return;
        _loaded = true;
        _lastTickUtc = DateTime.UtcNow;
        _timer.Start();
        try
        {
            if (await _game.LoadLatestAsync().ConfigureAwait(true))
                SetStatus("Loaded the latest saved game.");
            else
                NewGame(SudokuDifficultySegment.Medium);
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
            NewGame(SudokuDifficultySegment.Medium);
        }
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _timer.Stop();
        _game.WriteRecovery();
    }

    private Control BuildLayout()
    {
        var header = new StackPanel
        {
            Margin = new Thickness(20, 16, 20, 8),
            Spacing = 3,
            Children =
            {
                new TextBlock
                {
                    Text = "CLASSIC PUZZLE",
                    Classes = { "eyebrow" },
                    Foreground = GraphicalProfile.AccentBrush,
                },
                new TextBlock
                {
                    Text = "Novolis Sudoku",
                    Classes = { "page-title" },
                    Foreground = GraphicalProfile.TextBrush,
                },
                new TextBlock
                {
                    Text = "A deterministic puzzle, a clear board, and help when you want it.",
                    Classes = { "body-copy" },
                    Foreground = GraphicalProfile.MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(20, 0, 20, 10),
            Orientation = Orientation.Horizontal,
        };
        foreach (var segment in _game.Segments)
        {
            var selectedSegment = segment.Segment;
            toolbar.Children.Add(
                ActionButton(
                    segment.DisplayName,
                    () => NewGame(selectedSegment)));
        }
        toolbar.Children.Add(ActionButton("Load latest", () => _ = LoadLatestAsync()));
        toolbar.Children.Add(ActionButton("Save", () => _ = SaveAsync()));
        toolbar.Children.Add(ActionButton("Hint", ApplyHint));
        toolbar.Children.Add(ActionButton("Undo", Undo));
        toolbar.Children.Add(ActionButton("Redo", Redo));

        BuildBoard();
        _boardCard = new Border
        {
            Child = _board,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
        };

        _sidePanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Card("CURRENT GAME", _gameInfo),
                Card("PROGRESS", _progress),
                BuildInputCard(),
            },
        };
        _narrowScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        _sideScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        _bodyHost = new Grid
        {
            Margin = new Thickness(12, 0, 12, 8),
        };
        _bodyHost.SizeChanged += (_, _) => ArrangeSudokuBody();
        ArrangeSudokuBody();

        _status.Margin = new Thickness(16, 4, 16, 10);
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.TextWrapping = TextWrapping.Wrap;

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
            || _sidePanel is null
            || _narrowScroll is null
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
                DetachFromParent(_sidePanel);
                _bodyHost.Children.Clear();
                _bodyHost.RowDefinitions.Clear();
                _bodyHost.ColumnDefinitions.Clear();
                _narrowScroll.Content = null;
                _sideScroll.Content = null;

                if (narrow)
                {
                    var stack = new StackPanel { Spacing = 12 };
                    stack.Children.Add(_boardCard);
                    stack.Children.Add(_sidePanel);
                    _narrowScroll.Content = stack;
                    _bodyHost.Children.Add(_narrowScroll);
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
                FitBoard(width - 28);
            else if (height > 0)
                FitBoard(Math.Min(width - 312, height) - 16);
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

        var side = Math.Clamp(available, 180, 690);
        var cell = side / SudokuConstants.Size;
        if (Math.Abs(_board.Width - side) < 0.5 && Math.Abs(_board.Height - side) < 0.5)
        {
            ApplyCellFonts();
            return;
        }

        // Absolute cell sizes — Star rows inside a ScrollViewer grow infinitely tall.
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
        var font = Math.Clamp(cell * 0.42, 12, 28);
        foreach (var button in _cellButtons)
        {
            if (button is null)
                continue;
            button.MinWidth = 0;
            button.MinHeight = 0;
            var notes = button.Content is string text && text.Contains(' ', StringComparison.Ordinal);
            button.FontSize = notes ? Math.Max(9, font * 0.45) : font;
        }
    }

    private void BuildBoard()
    {
        for (var index = 0; index < SudokuConstants.Size; index++)
        {
            _board.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _board.Width = 360;
        _board.Height = 360;
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
                Padding = new Thickness(1),
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

    private Control BuildInputCard()
    {
        var keypad = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
            _numberButtons[number] = button;
            Grid.SetRow(button, (number - 1) / 3);
            Grid.SetColumn(button, (number - 1) % 3);
            keypad.Children.Add(button);
        }

        _clearButton.Content = "Clear";
        _clearButton.MinHeight = 44;
        _clearButton.Margin = new Thickness(0, 0, 6, 6);
        _clearButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _clearButton.VerticalContentAlignment = VerticalAlignment.Center;
        _clearButton.Background = GraphicalProfile.RaisedBrush;
        _clearButton.Foreground = GraphicalProfile.TextBrush;
        _clearButton.Click += (_, _) => ApplyValue(SudokuConstants.Empty);
        Grid.SetRow(_clearButton, 3);
        Grid.SetColumn(_clearButton, 0);
        Grid.SetColumnSpan(_clearButton, 3);
        keypad.Children.Add(_clearButton);

        _notesButton.Content = "Notes off";
        _notesButton.MinHeight = 44;
        _notesButton.Margin = new Thickness(0, 0, 6, 6);
        _notesButton.Click += (_, _) =>
        {
            _notesMode = !_notesMode;
            _notesButton.Content = _notesMode ? "Notes on" : "Notes off";
            SetStatus(_notesMode ? "Notes mode enabled." : "Notes mode disabled.");
        };

        var check = ActionButton("Check selected", CheckSelected);
        return Card(
            "INPUT",
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Select a cell, then enter a value.",
                        Foreground = GraphicalProfile.MutedBrush,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    keypad,
                    new WrapPanel
                    {
                        Children = { _notesButton, check },
                    },
                },
            });
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
            Padding = new Thickness(14),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = new StackPanel
            {
                Spacing = 7,
                Children = { heading, content },
            },
        };
    }

    private static Button ActionButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 42,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(12, 7),
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.OnAccentFillBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void Refresh()
    {
        var session = _game.Current;
        if (session is null)
        {
            _gameInfo.Text = "No game loaded.";
            _progress.Text = "Start a new puzzle.";
            _status.Text = _message;
            return;
        }

        var report = session.Game.Difficulty;
        var segmentName = _game.CurrentSegment?.DisplayName ?? report.Difficulty.ToString();
        var puzzlePosition = _game.CurrentPuzzleNumber > 0
            ? $" · puzzle {_game.CurrentPuzzleNumber}/{_game.CurrentSegment?.Games.Count ?? 0}"
            : string.Empty;
        _gameInfo.Text =
            $"{segmentName}{puzzlePosition}"
            + Environment.NewLine
            + $"{report.Difficulty} · seed {session.Game.Seed}"
            + Environment.NewLine
            + $"{report.GivenCount} givens · score {report.Score}";
        _progress.Text =
            $"{SudokuHelpers.CountFilled(session.Entries)}/{SudokuConstants.CellCount} filled"
            + Environment.NewLine
            + $"Time {FormatElapsed(session.Elapsed)} · "
            + $"mistakes {session.Mistakes} · hints {session.HintsUsed}";
        if (session.IsCompleted)
            _message = "Puzzle complete. Start another game whenever you are ready.";

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
            : $"{_message} Storage warning: {_game.LastStorageError}";
    }

    private void NewGame(SudokuDifficultySegment segment)
    {
        try
        {
            _selectedCell = -1;
            _game.StartNew(segment);
            SetStatus($"{segment} puzzle loaded.");
        }
        catch (Exception exception)
        {
            SetStatus($"New game failed: {exception.Message}");
        }
    }

    private async Task LoadLatestAsync()
    {
        try
        {
            if (await _game.LoadLatestAsync().ConfigureAwait(true))
            {
                _selectedCell = -1;
                SetStatus("Loaded the latest saved game.");
            }
            else
            {
                SetStatus("No saved game exists yet.");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            SetStatus(await _game.SaveAsync().ConfigureAwait(true)
                ? "Game saved."
                : "There is no game to save.");
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
        }
    }

    private void ApplyHint()
    {
        var hint = _game.ApplyHint();
        if (hint is null)
        {
            SetStatus("No simple single is available. Check entries or use the solver report.");
            return;
        }

        _selectedCell = hint.CellIndex;
        SetStatus($"Hint: {hint.Reason}");
    }

    private void Undo() =>
        SetStatus(_game.Undo() ? "Undo applied." : "Nothing to undo.");

    private void Redo() =>
        SetStatus(_game.Redo() ? "Redo applied." : "Nothing to redo.");

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
                ? $"Note {value} toggled."
                : "Notes are only available in empty editable cells.");
            return;
        }

        _game.TrySetValue(_selectedCell, value, out var message);
        SetStatus(message);
    }

    private void CheckSelected()
    {
        var session = _game.Current;
        if (session is null || _selectedCell < 0)
        {
            SetStatus("Select a cell first.");
            return;
        }
        if (session.IsGiven(_selectedCell))
        {
            SetStatus("That cell is a given.");
            return;
        }

        var value = session.Entries.Get(_selectedCell);
        SetStatus(value == SudokuConstants.Empty
            ? "The selected cell is empty."
            : value == session.Solution.Get(_selectedCell)
                ? "That value is correct."
                : "That value is not correct.");
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
