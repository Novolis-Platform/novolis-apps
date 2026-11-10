using Novolis.Game.Sudoku;
using Sudoku.Core;
using Sudoku.Storage;

namespace Sudoku.App;

/// <summary>Coordinates the linear ladder campaign with local save + progress files.</summary>
public sealed class SudokuGameService
{
    private readonly SudokuSaveStore _store;
    private readonly SudokuProgressStore _progress;
    private readonly SudokuPuzzleCatalog _catalog;
    private readonly SudokuLadder _ladder;
    private bool _completionRecorded;

    /// <summary>Creates the service over local stores and the embedded dump.</summary>
    public SudokuGameService(
        SudokuSaveStore store,
        SudokuProgressStore progress,
        SudokuPuzzleCatalog? catalog = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _catalog = catalog ?? SudokuPuzzleCatalog.LoadDefault();
        _ladder = SudokuLadder.FromCatalog(_catalog);
    }

    /// <summary>Raised after the current session or progress changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Current player session, if one has been opened.</summary>
    public SudokuSession? Current { get; private set; }

    /// <summary>Campaign ladder (1–N).</summary>
    public SudokuLadder Ladder => _ladder;

    /// <summary>All five pre-generated difficulty segments.</summary>
    public IReadOnlyList<SudokuPuzzleSegment> Segments => _catalog.Segments;

    /// <summary>Current ladder step metadata.</summary>
    public SudokuLadderPuzzle? CurrentLadderStep { get; private set; }

    /// <summary>Segment containing the current pre-generated puzzle.</summary>
    public SudokuPuzzleSegment? CurrentSegment { get; private set; }

    /// <summary>Current pre-generated puzzle metadata, when available.</summary>
    public SudokuPuzzleDefinition? CurrentPuzzle { get; private set; }

    /// <summary>One-based ladder number of the current puzzle.</summary>
    public int CurrentLadderNumber => CurrentLadderStep?.Number ?? 0;

    /// <summary>Snapshot of campaign progress.</summary>
    public SudokuProgressRecord Progress => _progress.Snapshot;

    /// <summary>Most recent non-fatal storage error.</summary>
    public string? LastStorageError { get; private set; }

    /// <summary>Opens the player's continue point (unlocked current ladder number).</summary>
    public SudokuSession ContinueCampaign()
    {
        var progress = _progress.Snapshot;
        var number = Math.Clamp(progress.CurrentLadderNumber, 1, _ladder.Count);
        number = Math.Min(number, progress.HighestUnlockedNumber);
        return StartLadder(number);
    }

    /// <summary>Opens a specific unlocked ladder number.</summary>
    public SudokuSession StartLadder(int ladderNumber)
    {
        var progress = _progress.Snapshot;
        var unlockedThrough = Math.Max(1, progress.HighestUnlockedNumber);
        if (ladderNumber < 1 || ladderNumber > unlockedThrough)
            throw new InvalidOperationException($"Puzzle {ladderNumber} is still locked.");

        var step = _ladder.Get(ladderNumber);
        return StartStep(step, resumeIfSaved: true);
    }

    /// <summary>Moves to the previous unlocked ladder puzzle.</summary>
    public bool TryPrevious()
    {
        if (CurrentLadderNumber <= 1)
            return false;
        StartLadder(CurrentLadderNumber - 1);
        return true;
    }

    /// <summary>Moves to the next unlocked ladder puzzle.</summary>
    public bool TryNext()
    {
        var progress = _progress.Snapshot;
        if (CurrentLadderNumber <= 0 || CurrentLadderNumber >= progress.HighestUnlockedNumber)
            return false;
        StartLadder(CurrentLadderNumber + 1);
        return true;
    }

    /// <summary>Starts the next pre-generated game in a named segment (legacy entry).</summary>
    public SudokuSession StartNew(SudokuDifficultySegment segment)
    {
        var selected = _catalog.GetSegment(segment);
        if (selected.Games.Count == 0)
            throw new InvalidDataException($"Segment '{segment}' is empty.");
        if (!_ladder.TryGetByPuzzleId(selected.Games[0].Id, out var first) || first is null)
            throw new InvalidOperationException($"Segment '{segment}' is missing from the ladder.");

        // Jump to the first unlocked puzzle in that segment when possible.
        var progress = _progress.Snapshot;
        for (var index = 0; index < selected.Games.Count; index++)
        {
            if (_ladder.TryGetByPuzzleId(selected.Games[index].Id, out var step)
                && step is not null
                && step.Number <= progress.HighestUnlockedNumber)
            {
                return StartLadder(step.Number);
            }
        }

        return StartLadder(Math.Min(first.Number, progress.HighestUnlockedNumber));
    }

    /// <summary>Loads the newest named save, mapping it onto the ladder when possible.</summary>
    public async ValueTask<bool> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var session = await _store.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
            return false;

        Current = session;
        _completionRecorded = session.IsCompleted;
        if (_catalog.TryFind(session.Puzzle, out var segment, out var definition)
            && segment is not null
            && definition is not null
            && _ladder.TryGetByPuzzleId(definition.Id, out var step)
            && step is not null)
        {
            CurrentSegment = segment;
            CurrentPuzzle = definition;
            CurrentLadderStep = step;
            _progress.SetCurrentLadderNumber(step.Number, Progress.HighestUnlockedNumber);
        }
        else
        {
            CurrentSegment = null;
            CurrentPuzzle = null;
            CurrentLadderStep = null;
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
        {
            NotifyChanged(writeRecovery: true);
            TryRecordCompletion();
        }

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

    private SudokuSession StartStep(SudokuLadderPuzzle step, bool resumeIfSaved)
    {
        var selected = _catalog.GetSegment(step.Segment);
        var definition = selected.Games[step.IndexInSegment];
        var sessionId = SudokuLadder.StableSessionId(definition.Id);

        if (resumeIfSaved)
        {
            var resumed = _store.LoadAsync(sessionId).AsTask().GetAwaiter().GetResult();
            if (resumed is not null && !resumed.IsCompleted)
            {
                Current = resumed;
                CurrentSegment = selected;
                CurrentPuzzle = definition;
                CurrentLadderStep = step;
                _completionRecorded = false;
                _progress.SetCurrentLadderNumber(step.Number, Progress.HighestUnlockedNumber);
                NotifyChanged(writeRecovery: false);
                return Current;
            }
        }

        var game = definition.CreateGame(new SudokuDifficultyAnalyzer());
        Current = SudokuSession.Create(sessionId, game);
        CurrentSegment = selected;
        CurrentPuzzle = definition;
        CurrentLadderStep = step;
        _completionRecorded = false;
        _progress.SetCurrentLadderNumber(step.Number, Progress.HighestUnlockedNumber);
        NotifyChanged(writeRecovery: true);
        return Current;
    }

    private void TryRecordCompletion()
    {
        if (_completionRecorded
            || Current is null
            || !Current.IsCompleted
            || CurrentLadderStep is null
            || CurrentPuzzle is null)
            return;

        try
        {
            _progress.RecordCompletion(
                CurrentPuzzle.Id,
                CurrentLadderStep.Number,
                Current.Elapsed,
                Current.Mistakes,
                Current.HintsUsed,
                _ladder.Count);
            _ = _store.SaveAsync(Current, $"cleared · #{CurrentLadderStep.Number}").AsTask().GetAwaiter().GetResult();
            _completionRecorded = true;
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
}
