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
    private string _message = "Clear the board, or head back to the ladder.";
    private Grid? _bodyHost;
    private Border? _boardCard;
    private Control? _keypadStrip;
    private StackPanel? _sidePanel;
    private ScrollViewer? _sideScroll;
    private readonly Button _backButton = new();

    /// <summary>Raised when the player wants the ladder home list again.</summary>
    public event EventHandler? BackToLadderRequested;

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
        if (_game.Current is not null)
            SetStatus($"Puzzle {_game.CurrentLadderNumber} of {_game.Ladder.Count}.");
        else
            SetStatus("No puzzle loaded — go back to the ladder.");
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_loaded)
            return;
        _loaded = true;
        _lastTickUtc = DateTime.UtcNow;
        _timer.Start();
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
        ConfigureChromeButton(_backButton, "Ladder", () =>
            BackToLadderRequested?.Invoke(this, EventArgs.Empty));
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
        toolbar.Children.Add(_backButton);
        toolbar.Children.Add(_prevButton);
        toolbar.Children.Add(_nextButton);
        toolbar.Children.Add(ChromeButton("Undo", Undo));
        toolbar.Children.Add(ChromeButton("Redo", Redo));

        BuildBoard();
        _boardCard = new Border
        {
            Child = _board,
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
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

        _bodyHost = new Grid { Margin = new Thickness(4, 0, 4, 2) };
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
                    // Board takes leftover height; keypad stays full-width under it.
                    _bodyHost.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                    _bodyHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    _boardCard.VerticalAlignment = VerticalAlignment.Center;
                    _boardCard.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _bodyHost.Children.Add(_boardCard);
                    Grid.SetRow(_keypadStrip, 1);
                    _keypadStrip.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _bodyHost.Children.Add(_keypadStrip);
                }
                else
                {
                    _boardCard.VerticalAlignment = VerticalAlignment.Top;
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
                // Use nearly the full phone width; only shrink if height cannot fit the square.
                var keypadReserve = _keypadStrip.Bounds.Height > 0
                    ? _keypadStrip.Bounds.Height + 4
                    : 168;
                var byWidth = Math.Max(240, width - 4);
                var byHeight = height > keypadReserve
                    ? height - keypadReserve
                    : byWidth;
                FitBoard(Math.Min(byWidth, byHeight));
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
        var font = Math.Clamp(cell * 0.48, 14, 34);
        var noteFont = Math.Clamp(cell * 0.26, 10, 18);
        foreach (var button in _cellButtons)
        {
            if (button is null)
                continue;
            button.MinWidth = 0;
            button.MinHeight = 0;
            if (button.Content is Grid notesGrid)
            {
                foreach (var child in notesGrid.Children.OfType<TextBlock>())
                    child.FontSize = noteFont;
                continue;
            }

            button.FontSize = font;
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
        // 3 tall × 4 wide: digits in the left 3 columns, commands on the right.
        var pad = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*"),
            RowDefinitions = RowDefinitions.Parse("*,*,*"),
            Margin = new Thickness(0, 2, 0, 0),
            MinHeight = 168,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var number = value;
            var button = CreatePadButton(number.ToString(), () => ApplyValue(number));
            button.FontWeight = FontWeight.SemiBold;
            button.FontSize = 22;
            button.MinHeight = 52;
            _numberButtons[number] = button;
            Grid.SetRow(button, (number - 1) / 3);
            Grid.SetColumn(button, (number - 1) % 3);
            pad.Children.Add(button);
        }

        StyleNotesCommandButton();
        _notesButton.Click += (_, _) =>
        {
            _notesMode = !_notesMode;
            StyleNotesCommandButton();
            SetStatus(_notesMode ? "Notes on — tap digits as pencil marks." : "Notes off.");
        };
        Grid.SetRow(_notesButton, 0);
        Grid.SetColumn(_notesButton, 3);
        pad.Children.Add(_notesButton);

        _clearButton.Content = "⌫";
        StyleCommandPadButton(_clearButton);
        _clearButton.Click += (_, _) => ApplyValue(SudokuConstants.Empty);
        Grid.SetRow(_clearButton, 1);
        Grid.SetColumn(_clearButton, 3);
        pad.Children.Add(_clearButton);

        var hint = CreatePadButton("Hint", ApplyHint);
        StyleCommandPadButton(hint);
        Grid.SetRow(hint, 2);
        Grid.SetColumn(hint, 3);
        pad.Children.Add(hint);

        return new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = pad,
        };
    }

    private Control BuildInputCardDesktop()
    {
        // Same 3×4 layout as the phone pad (digits left, commands right).
        var keypad = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
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
            Grid.SetRow(button, (number - 1) / 3);
            Grid.SetColumn(button, (number - 1) % 3);
            keypad.Children.Add(button);
        }

        var notes = new Button
        {
            MinHeight = 44,
            Margin = new Thickness(0, 0, 0, 6),
            Background = GraphicalProfile.SurfaceBrush,
            Content = CreateNotesFace(active: false),
        };
        notes.Click += (_, _) =>
        {
            _notesMode = !_notesMode;
            notes.Content = CreateNotesFace(_notesMode);
            notes.Background = _notesMode
                ? GraphicalProfile.RaisedBrush
                : GraphicalProfile.SurfaceBrush;
            SetStatus(_notesMode ? "Notes on — tap digits as superscript marks." : "Notes off.");
        };
        Grid.SetRow(notes, 0);
        Grid.SetColumn(notes, 3);
        keypad.Children.Add(notes);

        var clear = new Button
        {
            Content = "⌫",
            MinHeight = 44,
            Margin = new Thickness(0, 0, 0, 6),
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        clear.Click += (_, _) => ApplyValue(SudokuConstants.Empty);
        Grid.SetRow(clear, 1);
        Grid.SetColumn(clear, 3);
        keypad.Children.Add(clear);

        var hint = new Button
        {
            Content = "Hint",
            MinHeight = 44,
            Margin = new Thickness(0, 0, 0, 6),
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        hint.Click += (_, _) => ApplyHint();
        Grid.SetRow(hint, 2);
        Grid.SetColumn(hint, 3);
        keypad.Children.Add(hint);

        return Card("INPUT", keypad);
    }

    private static Button CreatePadButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 52,
            MinWidth = 0,
            Margin = new Thickness(3),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static void StyleCommandPadButton(Button button)
    {
        button.MinHeight = 52;
        button.MinWidth = 0;
        button.Margin = new Thickness(3);
        button.Padding = new Thickness(0);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.VerticalAlignment = VerticalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Background = GraphicalProfile.RaisedBrush;
        button.Foreground = GraphicalProfile.TextBrush;
        button.FontSize = 15;
    }

    private void StyleNotesCommandButton()
    {
        StyleCommandPadButton(_notesButton);
        _notesButton.Content = CreateNotesFace(_notesMode);
        _notesButton.Background = _notesMode
            ? GraphicalProfile.RaisedBrush
            : GraphicalProfile.SurfaceBrush;
        _notesButton.BorderBrush = GraphicalProfile.BorderBrush;
        _notesButton.BorderThickness = new Thickness(1);
    }

    private static TextBlock CreateNotesFace(bool active) =>
        new()
        {
            Text = "¹²³",
            FontSize = active ? 15 : 13,
            FontWeight = FontWeight.Normal,
            Foreground = active
                ? GraphicalProfile.AccentBrush
                : GraphicalProfile.MutedBrush,
            Opacity = active ? 1 : 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

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
            if (value == SudokuConstants.Empty)
            {
                var marks = session.GetNotes(cellIndex).Values().ToArray();
                button.Content = marks.Length == 0
                    ? string.Empty
                    : BuildNotesGrid(marks);
            }
            else
            {
                button.Content = value.ToString();
            }
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

    /// <summary>Classic 3×3 pencil-mark layout filling the cell (not a centered cluster).</summary>
    private static Grid BuildNotesGrid(IReadOnlyList<byte> marks)
    {
        var present = marks.ToHashSet();
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(1),
        };
        for (var index = 0; index < 3; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (byte value = 1; value <= SudokuConstants.SymbolCount; value++)
        {
            var face = new TextBlock
            {
                Text = present.Contains(value) ? value.ToString() : string.Empty,
                FontSize = 12,
                Foreground = GraphicalProfile.MutedBrush,
                Opacity = 0.9,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetRow(face, (value - 1) / 3);
            Grid.SetColumn(face, (value - 1) % 3);
            grid.Children.Add(face);
        }

        return grid;
    }
}
