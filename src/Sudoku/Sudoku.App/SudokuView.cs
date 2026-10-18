using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Game.Sudoku;

namespace Sudoku.App;

/// <summary>Code-only interactive classic Sudoku board and command surface.</summary>
public sealed class SudokuView : UserControl
{
    private readonly SudokuGameService _game;
    private readonly Grid _board = new();
    private readonly Button[] _cellButtons = new Button[SudokuConstants.CellCount];
    private readonly TextBlock _status = new();
    private readonly TextBlock _progress = new();
    private readonly TextBlock _gameInfo = new();
    private readonly Button _notesButton = new();
    private readonly DispatcherTimer _timer;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private int _selectedCell = -1;
    private bool _notesMode;
    private bool _loaded;
    private string _message = "Choose a difficulty to begin.";

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
                NewGame(SudokuDifficulty.Medium);
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
            NewGame(SudokuDifficulty.Medium);
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
                },
            },
        };

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(20, 0, 20, 10),
            Orientation = Orientation.Horizontal,
        };
        toolbar.Children.Add(ActionButton("Easy", () => NewGame(SudokuDifficulty.Easy)));
        toolbar.Children.Add(ActionButton("Medium", () => NewGame(SudokuDifficulty.Medium)));
        toolbar.Children.Add(ActionButton("Hard", () => NewGame(SudokuDifficulty.Hard)));
        toolbar.Children.Add(ActionButton("Expert", () => NewGame(SudokuDifficulty.Expert)));
        toolbar.Children.Add(ActionButton("Load latest", () => _ = LoadLatestAsync()));
        toolbar.Children.Add(ActionButton("Save", () => _ = SaveAsync()));
        toolbar.Children.Add(ActionButton("Hint", ApplyHint));
        toolbar.Children.Add(ActionButton("Undo", Undo));
        toolbar.Children.Add(ActionButton("Redo", Redo));

        BuildBoard();
        var boardCard = new Border
        {
            Child = _board,
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
        };

        var side = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 20, 0),
            Children =
            {
                Card("CURRENT GAME", _gameInfo),
                Card("PROGRESS", _progress),
                BuildInputCard(),
            },
        };
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,330"),
            Margin = new Thickness(20, 0, 0, 10),
        };
        body.Children.Add(new ScrollViewer
        {
            Content = boardCard,
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        });
        Grid.SetColumn(side, 1);
        body.Children.Add(new ScrollViewer
        {
            Content = side,
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        });
        Grid.SetColumn(body.Children[^1], 1);

        _status.Margin = new Thickness(20, 4, 20, 14);
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.TextWrapping = TextWrapping.Wrap;

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(toolbar);
        root.Children.Add(_status);
        root.Children.Add(body);
        return root;
    }

    private void BuildBoard()
    {
        for (var index = 0; index < SudokuConstants.Size; index++)
        {
            _board.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _board.MaxWidth = 690;
        _board.MaxHeight = 690;
        _board.HorizontalAlignment = HorizontalAlignment.Center;
        _board.VerticalAlignment = VerticalAlignment.Top;
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            var cell = SudokuCell.FromIndex(cellIndex);
            var button = new Button
            {
                Tag = cellIndex,
                MinWidth = 48,
                MinHeight = 48,
                Padding = new Thickness(2),
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
        var keypad = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var number = value;
            var button = new Button
            {
                Content = number.ToString(),
                Width = 52,
                MinHeight = 44,
                Margin = new Thickness(0, 0, 6, 6),
                Background = GraphicalProfile.RaisedBrush,
                Foreground = GraphicalProfile.TextBrush,
            };
            button.Click += (_, _) => ApplyValue(number);
            keypad.Children.Add(button);
        }

        var clear = ActionButton("Clear", () => ApplyValue(SudokuConstants.Empty));
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
                        Children = { clear, _notesButton, check },
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
            Background = GraphicalProfile.ActionBrush,
            Foreground = GraphicalProfile.OnActionBrush,
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
        _gameInfo.Text =
            $"{report.Difficulty} · seed {session.Game.Seed}"
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
            button.Background = isSelected
                ? GraphicalProfile.ActionBrush
                : isWrong || conflicts.Contains(cellIndex)
                    ? GraphicalProfile.DangerBrush
                    : isRelated
                        ? GraphicalProfile.ActionSoftBrush
                        : isGiven
                            ? GraphicalProfile.RaisedBrush
                            : GraphicalProfile.SurfaceBrush;
            button.Foreground = isSelected
                ? GraphicalProfile.OnActionBrush
                : isWrong || conflicts.Contains(cellIndex)
                    ? GraphicalProfile.TextBrush
                    : isGiven
                        ? GraphicalProfile.AccentBrush
                        : GraphicalProfile.TextBrush;
            button.FontWeight = isGiven ? FontWeight.Bold : FontWeight.Normal;
            button.FontSize = value == SudokuConstants.Empty ? 12 : 25;
            button.Content = value == SudokuConstants.Empty
                ? string.Join(" ", session.GetNotes(cellIndex).Values())
                : value.ToString();
        }

        _status.Text = _game.LastStorageError is null
            ? _message
            : $"{_message} Storage warning: {_game.LastStorageError}";
    }

    private void NewGame(SudokuDifficulty difficulty)
    {
        try
        {
            _selectedCell = -1;
            _game.StartNew(difficulty);
            SetStatus($"{difficulty} puzzle generated.");
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
