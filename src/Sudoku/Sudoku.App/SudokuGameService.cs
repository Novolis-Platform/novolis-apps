using Novolis.Game.Sudoku;
using Sudoku.Core;
using Sudoku.Storage;

namespace Sudoku.App;

/// <summary>Coordinates a current Sudoku session with generation and storage.</summary>
public sealed class SudokuGameService
{
    private readonly SudokuGenerator _generator = new();
    private readonly SudokuSaveStore _store;

    /// <summary>Creates the service over the local save store.</summary>
    public SudokuGameService(SudokuSaveStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Raised after the current session changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Current player session, if one has been opened.</summary>
    public SudokuSession? Current { get; private set; }

    /// <summary>Most recent non-fatal storage error.</summary>
    public string? LastStorageError { get; private set; }

    /// <summary>Starts a deterministic session for a new seed.</summary>
    public SudokuSession StartNew(
        SudokuDifficulty difficulty,
        ulong? seed = null)
    {
        var selectedSeed = seed
            ?? unchecked((ulong)DateTime.UtcNow.Ticks);
        var profile = SudokuDifficultyProfile.For(difficulty);
        var game = _generator.Generate(
            selectedSeed,
            difficulty,
            new SudokuGenerationOptions
            {
                Profile = profile,
                MaximumAttempts = 16,
                RequireProfileMatch = false,
            });
        Current = SudokuSession.Create(Guid.CreateVersion7(), game);
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
}
