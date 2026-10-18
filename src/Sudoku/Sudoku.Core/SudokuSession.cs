using Novolis.Game.Sudoku;

namespace Sudoku.Core;

/// <summary>
/// Player session state over a generated puzzle. The class contains no UI or
/// file-system dependencies, so it can be reused by another host later.
/// </summary>
public sealed class SudokuSession
{
    private readonly Stack<SessionMove> _undo = [];
    private readonly Stack<SessionMove> _redo = [];

    private SudokuSession(
        Guid id,
        SudokuGeneratedGame game,
        SudokuBoard entries,
        CandidateMask[] notes,
        TimeSpan elapsed,
        int mistakes,
        int hintsUsed,
        bool completed)
    {
        Id = id;
        Game = game;
        Entries = entries;
        Notes = notes;
        Elapsed = elapsed;
        Mistakes = mistakes;
        HintsUsed = hintsUsed;
        IsCompleted = completed;
    }

    /// <summary>Creates a new player session from a generated game.</summary>
    public static SudokuSession Create(Guid id, SudokuGeneratedGame game)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Session id must not be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(game);
        return new SudokuSession(
            id,
            game,
            game.Puzzle.Clone(),
            new CandidateMask[SudokuConstants.CellCount],
            TimeSpan.Zero,
            mistakes: 0,
            hintsUsed: 0,
            completed: false);
    }

    /// <summary>Restores a saved player session.</summary>
    public static SudokuSession Restore(
        Guid id,
        SudokuGeneratedGame game,
        SudokuBoard entries,
        IEnumerable<CandidateMask> notes,
        TimeSpan elapsed,
        int mistakes,
        int hintsUsed,
        bool completed)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(notes);
        if (id == Guid.Empty)
            throw new ArgumentException("Session id must not be empty.", nameof(id));
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (mistakes < 0)
            throw new ArgumentOutOfRangeException(nameof(mistakes));
        if (hintsUsed < 0)
            throw new ArgumentOutOfRangeException(nameof(hintsUsed));

        var noteArray = notes.ToArray();
        if (noteArray.Length != SudokuConstants.CellCount)
            throw new ArgumentException("A session must contain 81 note masks.", nameof(notes));
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            if (game.Puzzle.Get(cellIndex) != SudokuConstants.Empty
                && game.Puzzle.Get(cellIndex) != entries.Get(cellIndex))
            {
                throw new ArgumentException(
                    "Saved entries must preserve every given value.",
                    nameof(entries));
            }
        }

        return new SudokuSession(
            id,
            game,
            entries.Clone(),
            noteArray,
            elapsed,
            mistakes,
            hintsUsed,
            completed && IsSolvedAgainst(entries, game.Solution));
    }

    /// <summary>Stable identity of the saved session.</summary>
    public Guid Id { get; }

    /// <summary>Generated puzzle, solution, and measured difficulty.</summary>
    public SudokuGeneratedGame Game { get; }

    /// <summary>Puzzle givens.</summary>
    public SudokuBoard Puzzle => Game.Puzzle;

    /// <summary>Canonical solution retained for checking and completion.</summary>
    public SudokuBoard Solution => Game.Solution;

    /// <summary>Current player-visible values, including givens.</summary>
    public SudokuBoard Entries { get; }

    /// <summary>Candidate notes per cell.</summary>
    public CandidateMask[] Notes { get; }

    /// <summary>Accumulated play time.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Number of incorrect values entered.</summary>
    public int Mistakes { get; private set; }

    /// <summary>Number of hints applied.</summary>
    public int HintsUsed { get; private set; }

    /// <summary>Whether all entries match the canonical solution.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Returns true when a cell is a puzzle given.</summary>
    public bool IsGiven(int cellIndex)
    {
        SudokuConstants.ValidateCellIndex(cellIndex);
        return Puzzle.Get(cellIndex) != SudokuConstants.Empty;
    }

    /// <summary>Returns notes for a cell.</summary>
    public CandidateMask GetNotes(int cellIndex)
    {
        SudokuConstants.ValidateCellIndex(cellIndex);
        return Notes[cellIndex];
    }

    /// <summary>Accumulates positive elapsed time.</summary>
    public void AddElapsed(TimeSpan elapsed)
    {
        if (elapsed > TimeSpan.Zero)
            Elapsed += elapsed;
    }

    /// <summary>Places or clears a player value.</summary>
    public bool TrySetValue(int cellIndex, byte value, out string message)
    {
        SudokuConstants.ValidateCellIndex(cellIndex);
        SudokuConstants.ValidateValue(value);
        if (IsGiven(cellIndex))
        {
            message = "Given cells cannot be changed.";
            return false;
        }

        var previousValue = Entries.Get(cellIndex);
        var previousNotes = Notes[cellIndex];
        if (previousValue == value)
        {
            message = "That value is already entered.";
            return false;
        }

        Entries.Set(cellIndex, value);
        Notes[cellIndex] = CandidateMask.None;
        if (value != SudokuConstants.Empty && value != Solution.Get(cellIndex))
            Mistakes++;

        PushMove(new SessionMove(
            cellIndex,
            previousValue,
            value,
            previousNotes,
            CandidateMask.None));
        IsCompleted = IsSolvedAgainst(Entries, Solution);
        message = IsCompleted
            ? "Puzzle complete."
            : value == SudokuConstants.Empty
                ? "Cell cleared."
                : value == Solution.Get(cellIndex)
                    ? "Value entered."
                    : "Value entered; check the highlighted cell.";
        return true;
    }

    /// <summary>Toggles a candidate note in an empty editable cell.</summary>
    public bool ToggleNote(int cellIndex, byte value)
    {
        SudokuConstants.ValidateCellIndex(cellIndex);
        if (value is < 1 or > SudokuConstants.SymbolCount
            || IsGiven(cellIndex)
            || Entries.Get(cellIndex) != SudokuConstants.Empty)
        {
            return false;
        }

        var previousNotes = Notes[cellIndex];
        var nextNotes = previousNotes.Contains(value)
            ? previousNotes.Remove(value)
            : previousNotes.Add(value);
        Notes[cellIndex] = nextNotes;
        PushMove(new SessionMove(
            cellIndex,
            Entries.Get(cellIndex),
            Entries.Get(cellIndex),
            previousNotes,
            nextNotes));
        return true;
    }

    /// <summary>Applies a logical hint and records it in the session.</summary>
    public SudokuHint? ApplyHint()
    {
        var hint = FindHint();
        if (hint is null)
            return null;

        if (TrySetValue(hint.CellIndex, hint.Value, out _))
            HintsUsed++;
        return hint;
    }

    /// <summary>Finds a hint while ignoring incorrect editable entries.</summary>
    public SudokuHint? FindHint()
    {
        var hintBoard = Puzzle.Clone();
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            var entry = Entries.Get(cellIndex);
            if (!IsGiven(cellIndex) && entry != SudokuConstants.Empty
                && entry == Solution.Get(cellIndex))
            {
                hintBoard.Set(cellIndex, entry);
            }
        }

        return new SudokuHintProvider().FindNext(hintBoard);
    }

    /// <summary>Undoes the most recent value or note change.</summary>
    public bool Undo()
    {
        if (!_undo.TryPop(out var move))
            return false;
        ApplyMove(move, undo: true);
        _redo.Push(move);
        return true;
    }

    /// <summary>Redoes the most recently undone value or note change.</summary>
    public bool Redo()
    {
        if (!_redo.TryPop(out var move))
            return false;
        ApplyMove(move, undo: false);
        _undo.Push(move);
        return true;
    }

    private void PushMove(SessionMove move)
    {
        _undo.Push(move);
        _redo.Clear();
    }

    private void ApplyMove(SessionMove move, bool undo)
    {
        Entries.Set(move.CellIndex, undo ? move.PreviousValue : move.NextValue);
        Notes[move.CellIndex] = undo ? move.PreviousNotes : move.NextNotes;
        IsCompleted = IsSolvedAgainst(Entries, Solution);
    }

    private static bool IsSolvedAgainst(SudokuBoard entries, SudokuBoard solution)
    {
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
            if (entries.Get(cellIndex) != solution.Get(cellIndex))
                return false;
        return true;
    }

    private sealed record SessionMove(
        int CellIndex,
        byte PreviousValue,
        byte NextValue,
        CandidateMask PreviousNotes,
        CandidateMask NextNotes);
}
