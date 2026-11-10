using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Sudoku.Core;
using Sudoku.Storage;

namespace Sudoku.App;

/// <summary>Ladder catalog home: continue/resume plus open steps with active-game badges.</summary>
public sealed class SudokuHomeView : UserControl
{
    private readonly SudokuGameService _game;
    private readonly Action<SudokuPlayRequest> _play;
    private readonly TextBlock _status = new();

    /// <summary>Creates the home list.</summary>
    public SudokuHomeView(
        SudokuGameService game,
        Action<SudokuPlayRequest> play,
        string? statusMessage = null)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _play = play ?? throw new ArgumentNullException(nameof(play));
        Content = BuildLayout();
        if (!string.IsNullOrWhiteSpace(statusMessage))
            _status.Text = statusMessage;
    }

    private Control BuildLayout()
    {
        var progress = _game.Progress;
        var activeByNumber = _game.IncompleteSavesByLadderNumber();
        var primaryActive = _game.TryGetPrimaryActiveGame();

        var title = new TextBlock
        {
            Text = "Novolis Sudoku",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = GraphicalProfile.TextBrush,
        };
        var subtitle = new TextBlock
        {
            Text =
                $"Ladder · all {_game.Ladder.Count} open"
                + $" · cleared {progress.ConqueredPuzzleIds.Count}/{_game.Ladder.Count}"
                + (activeByNumber.Count > 0
                    ? $" · {activeByNumber.Count} in progress"
                    : string.Empty),
            FontSize = 13,
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var continueButton = new Button
        {
            Content = primaryActive is { } active
                ? $"Resume · #{active.Step.Number} · {FormatElapsed(TimeSpan.FromSeconds(active.Save.ElapsedSeconds))}"
                : $"Continue · #{Math.Clamp(progress.CurrentLadderNumber, 1, _game.Ladder.Count)}",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 44,
            Margin = new Thickness(0, 12, 0, 8),
        };
        continueButton.Click += (_, _) =>
        {
            if (primaryActive is { } resume)
                _play(SudokuPlayRequest.Ladder(resume.Step.Number));
            else
                _play(SudokuPlayRequest.Continue());
        };

        var list = new StackPanel { Spacing = 4 };
        SudokuDifficultySegment? lastSegment = null;
        foreach (var step in _game.Ladder.Steps)
        {
            if (lastSegment != step.Segment)
            {
                lastSegment = step.Segment;
                list.Children.Add(new TextBlock
                {
                    Text = step.SegmentDisplayName.ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GraphicalProfile.MutedBrush,
                    Margin = new Thickness(4, 12, 4, 4),
                });
            }

            activeByNumber.TryGetValue(step.Number, out var activeSave);
            list.Children.Add(BuildStepRow(step, progress, activeSave));
        }

        _status.FontSize = 12;
        _status.Foreground = GraphicalProfile.MutedBrush;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 8, 0, 0);
        if (string.IsNullOrWhiteSpace(_status.Text))
        {
            _status.Text = primaryActive is { } resumeHint
                ? $"Active game on #{resumeHint.Step.Number} · {FormatElapsed(TimeSpan.FromSeconds(resumeHint.Save.ElapsedSeconds))} — tap Resume or the ladder row."
                : "Pick a puzzle from the ladder, or continue where you left off.";
        }

        var header = new StackPanel
        {
            Margin = new Thickness(16, 16, 16, 8),
            Children = { title, subtitle, continueButton, _status },
        };

        var scroll = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(12, 0, 12, 16),
                Children = { list },
            },
            HorizontalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(scroll);
        Background = GraphicalProfile.BackgroundBrush;
        return root;
    }

    private Control BuildStepRow(
        SudokuLadderPuzzle step,
        SudokuProgressRecord progress,
        SudokuSaveRecord? activeSave)
    {
        var conquered = progress.ConqueredPuzzleIds.Contains(
            step.PuzzleId,
            StringComparer.OrdinalIgnoreCase);
        progress.BestTimesSeconds.TryGetValue(step.PuzzleId, out var bestSeconds);
        var inProgress = activeSave is not null;

        var state = inProgress
            ? $"In progress · {FormatElapsed(TimeSpan.FromSeconds(activeSave!.ElapsedSeconds))}"
            : conquered
                ? bestSeconds > 0
                    ? $"Cleared · best {FormatElapsed(TimeSpan.FromSeconds(bestSeconds))}"
                    : "Cleared"
                : step.Number == progress.CurrentLadderNumber
                    ? "Current"
                    : "Open";

        var title = new TextBlock
        {
            Text = $"#{step.Number} · {step.SegmentDisplayName}",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
        };
        var detail = new TextBlock
        {
            Text = state,
            FontSize = 12,
            Foreground = inProgress
                ? GraphicalProfile.AccentBrush
                : GraphicalProfile.MutedBrush,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var row = new Border
        {
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(10),
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = inProgress
                ? GraphicalProfile.AccentBrush
                : GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(inProgress ? 2 : 1),
            Child = new StackPanel { Children = { title, detail } },
        };

        row.Cursor = new Cursor(StandardCursorType.Hand);
        // Press-to-open steals ScrollViewer drags on touch — only open on a short tap.
        AttachScrollFriendlyTap(row, () => _play(SudokuPlayRequest.Ladder(step.Number)));
        return row;
    }

    private static void AttachScrollFriendlyTap(Control target, Action onTap)
    {
        const double cancelDistance = 12;
        Point? originRoot = null;

        target.PointerPressed += (_, args) =>
        {
            var point = args.GetCurrentPoint(target);
            if (!point.Properties.IsLeftButtonPressed)
                return;
            // Root space: ScrollViewer can move the row under the finger without local Moved events.
            originRoot = args.GetPosition(null);
        };
        target.PointerReleased += (_, args) =>
        {
            if (originRoot is not { } start)
                return;
            originRoot = null;
            var end = args.GetPosition(null);
            if (Math.Abs(end.X - start.X) >= cancelDistance
                || Math.Abs(end.Y - start.Y) >= cancelDistance)
                return;
            onTap();
        };
        target.PointerCaptureLost += (_, _) => originRoot = null;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        return $"{elapsed.Minutes}:{elapsed.Seconds:D2}";
    }
}
