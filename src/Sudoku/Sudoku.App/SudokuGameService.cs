using Novolis.Game.Sudoku;
using Sudoku.Core;
using Sudoku.Storage;

namespace Sudoku.App;

/// <summary>Coordinates a current Sudoku session with generation and storage.</summary>
public sealed class SudokuGameService
{
    private readonly SudokuSaveStore _store;
    private readonly SudokuPuzzleCatalog _catalog;
    private readonly Dictionary<SudokuDifficultySegment, int> _nextPuzzle = [];

    /// <summary>Creates the service over the local save store.</summary>
    public SudokuGameService(
        SudokuSaveStore store,
        SudokuPuzzleCatalog? catalog = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? SudokuPuzzleCatalog.LoadDefault();
        foreach (var segment in Enum.GetValues<SudokuDifficultySegment>())
            _nextPuzzle[segment] = 0;
    }

    /// <summary>Raised after the current session changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Current player session, if one has been opened.</summary>
    public SudokuSession? Current { get; private set; }

    /// <summary>All five pre-generated difficulty segments.</summary>
    public IReadOnlyList<SudokuPuzzleSegment> Segments => _catalog.Segments;

    /// <summary>Segment containing the current pre-generated puzzle.</summary>
    public SudokuPuzzleSegment? CurrentSegment { get; private set; }

    /// <summary>Current pre-generated puzzle metadata, when available.</summary>
    public SudokuPuzzleDefinition? CurrentPuzzle { get; private set; }

    /// <summary>One-based position of the current puzzle within its segment.</summary>
    public int CurrentPuzzleNumber { get; private set; }

    /// <summary>Most recent non-fatal storage error.</summary>
    public string? LastStorageError { get; private set; }

    /// <summary>Starts the next pre-generated game in a named segment.</summary>
    public SudokuSession StartNew(SudokuDifficultySegment segment)
    {
        var selected = _catalog.GetSegment(segment);
        var index = _nextPuzzle[segment] % selected.Games.Count;
        _nextPuzzle[segment] = (index + 1) % selected.Games.Count;
        return StartNewAt(segment, index);
    }

    /// <summary>
    /// Starts a pre-generated game from an engine difficulty band. The optional
    /// seed selects a stable position within that band's segment.
    /// </summary>
    public SudokuSession StartNew(
        SudokuDifficulty difficulty,
        ulong? seed = null)
    {
        var segment = difficulty switch
        {
            SudokuDifficulty.Easy => SudokuDifficultySegment.Easy,
            SudokuDifficulty.Medium => SudokuDifficultySegment.Medium,
            SudokuDifficulty.Hard => SudokuDifficultySegment.Hard,
            SudokuDifficulty.Expert => SudokuDifficultySegment.Expert,
            _ => SudokuDifficultySegment.Medium,
        };
        if (seed is null)
            return StartNew(segment);

        var selected = _catalog.GetSegment(segment);
        var index = (int)(seed.Value % (ulong)selected.Games.Count);
        _nextPuzzle[segment] = (index + 1) % selected.Games.Count;
        return StartNewAt(segment, index);
    }

    private SudokuSession StartNewAt(
        SudokuDifficultySegment segment,
        int index)
    {
        var selected = _catalog.GetSegment(segment);
        var game = _catalog.CreateGame(segment, index);
        Current = SudokuSession.Create(Guid.CreateVersion7(), game);
        CurrentSegment = selected;
        CurrentPuzzle = selected.Games[index];
        CurrentPuzzleNumber = index + 1;
        NotifyChanged(writeRecovery: true);
        return Current;
    }

    /// <summary>Loads the newest named save.</summary>
    public async ValueTask<bool> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var session = await _store.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
            return false;
        Current = session;
        if (_catalog.TryFind(
                session.Puzzle,
                out var segment,
                out var definition)
            && segment is not null
            && definition is not null)
        {
            CurrentSegment = segment;
            CurrentPuzzle = definition;
            CurrentPuzzleNumber = FindPuzzleNumber(segment, definition) + 1;
        }
        else
        {
            CurrentSegment = null;
            CurrentPuzzle = null;
            CurrentPuzzleNumber = 0;
        }
        NotifyChanged(writeRecovery: false);
        return true;
    }

    /// <summary>Saves the current session to the normal named-save store.</summary>
    public async ValueTask<bool> SaveAsync(
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null)
            return false;
        await _store.SaveAsync(Current, label, cancellationToken).ConfigureAwait(false);
        LastStorageError = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Places or clears a value in the current session.</summary>
    public bool TrySetValue(int cellIndex, byte value, out string message)
    {
        if (Current is null)
        {
            message = "Start a game first.";
            return false;
        }

        var changed = Current.TrySetValue(cellIndex, value, out message);
        if (changed)
            NotifyChanged(writeRecovery: true);
        return changed;
    }

    /// <summary>Toggles a note in the current session.</summary>
    public bool ToggleNote(int cellIndex, byte value)
    {
        if (Current is null)
            return false;
        var changed = Current.ToggleNote(cellIndex, value);
        if (changed)
            NotifyChanged(writeRecovery: true);
        return changed;
    }

    /// <summary>Applies an explainable logical hint.</summary>
    public SudokuHint? ApplyHint()
    {
        if (Current is null)
            return null;
        var hint = Current.ApplyHint();
        if (hint is not null)
            NotifyChanged(writeRecovery: true);
        return hint;
    }

    /// <summary>Undoes the most recent session change.</summary>
    public bool Undo()
    {
        if (Current is null || !Current.Undo())
            return false;
        NotifyChanged(writeRecovery: true);
        return true;
    }

    /// <summary>Redoes the most recently undone session change.</summary>
    public bool Redo()
    {
        if (Current is null || !Current.Redo())
            return false;
        NotifyChanged(writeRecovery: true);
        return true;
    }

    /// <summary>Advances play time without writing on every timer tick.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (Current is null)
            return;
        Current.AddElapsed(elapsed);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes a recovery snapshot for the current session.</summary>
    public void WriteRecovery()
    {
        if (Current is null)
            return;
        try
        {
            _store.WriteRecovery(Current);
            LastStorageError = null;
        }
        catch (Exception exception)
        {
            LastStorageError = exception.Message;
        }
    }

    private void NotifyChanged(bool writeRecovery)
    {
        if (writeRecovery)
            WriteRecovery();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static int FindPuzzleNumber(
        SudokuPuzzleSegment segment,
        SudokuPuzzleDefinition definition)
    {
        for (var index = 0; index < segment.Games.Count; index++)
        {
            if (ReferenceEquals(segment.Games[index], definition)
                || string.Equals(
                    segment.Games[index].Id,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }
}
