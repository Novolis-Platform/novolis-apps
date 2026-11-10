using Avalonia.Controls;
using Avalonia.Threading;

namespace Sudoku.App;

/// <summary>Switches between the ladder home list and the board play view.</summary>
public sealed class SudokuShell : UserControl
{
    private readonly SudokuGameService _game;

    /// <summary>Creates the shell over the shared game service.</summary>
    public SudokuShell(SudokuGameService game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        ShowHome();
    }

    /// <summary>Shows the ladder list (cold start and back from play).</summary>
    public void ShowHome() =>
        Content = new SudokuHomeView(_game, OnPlayRequested);

    /// <summary>
    /// Returns true when play was open and back navigated to the ladder
    /// (so the Android host should not exit).
    /// </summary>
    public bool TryHandleBack()
    {
        if (Content is not SudokuView)
            return false;
        ShowHome();
        return true;
    }

    private void OnPlayRequested(SudokuPlayRequest request) =>
        _ = Dispatcher.UIThread.InvokeAsync(() => OpenPlayAsync(request));

    private async Task OpenPlayAsync(SudokuPlayRequest request)
    {
        try
        {
            switch (request.Kind)
            {
                case SudokuPlayKind.Continue:
                    if (!await _game.LoadLatestAsync().ConfigureAwait(true))
                        _game.ContinueCampaign();
                    break;
                case SudokuPlayKind.Ladder:
                    _game.StartLadder(request.LadderNumber);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown play kind '{request.Kind}'.");
            }
        }
        catch (Exception exception)
        {
            Content = new SudokuHomeView(_game, OnPlayRequested, exception.Message);
            return;
        }

        var play = new SudokuView(_game);
        play.BackToLadderRequested += (_, _) => ShowHome();
        Content = play;
    }
}
