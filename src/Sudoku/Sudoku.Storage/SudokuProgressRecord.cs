namespace Sudoku.Storage;

/// <summary>Local campaign progress and aggregate stats (JSON file).</summary>
public sealed class SudokuProgressRecord
{
    /// <summary>Current document schema.</summary>
    public const int CurrentSchema = 1;

    /// <summary>Schema version.</summary>
    public int SchemaVersion { get; set; } = CurrentSchema;

    /// <summary>1-based ladder number currently open (or next to play).</summary>
    public int CurrentLadderNumber { get; set; } = 1;

    /// <summary>Highest ladder number unlocked (completed + 1, minimum 1).</summary>
    public int HighestUnlockedNumber { get; set; } = 1;

    /// <summary>Dump puzzle ids that have been conquered at least once.</summary>
    public List<string> ConqueredPuzzleIds { get; set; } = [];

    /// <summary>Best completion times in seconds, keyed by dump puzzle id.</summary>
    public Dictionary<string, long> BestTimesSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Total completed clears (may exceed unique conquers on repeats).</summary>
    public int Completions { get; set; }

    /// <summary>Lifetime play seconds across sessions.</summary>
    public long TotalPlaySeconds { get; set; }

    /// <summary>Lifetime mistakes recorded.</summary>
    public int TotalMistakes { get; set; }

    /// <summary>Total hints used.</summary>
    public int TotalHints { get; set; }
}
