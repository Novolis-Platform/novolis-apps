using Novolis.Game.Sudoku;
using Novolis.Storage.Abstractions;

namespace Sudoku.Storage;

/// <summary>
/// Versioned local save document. The solution is kept with the puzzle so a
/// saved game can be checked without regenerating or relying on a future
/// generator implementation.
/// </summary>
public sealed class SudokuSaveRecord : IHasId
{
    /// <summary>Current persisted document schema.</summary>
    public const int CurrentSchema = 1;

    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>Schema version for migrations.</summary>
    public int SchemaVersion { get; set; } = CurrentSchema;

    /// <summary>User-visible save label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Last save timestamp.</summary>
    public DateTimeOffset SavedUtc { get; set; }

    /// <summary>Ruleset identity.</summary>
    public string RulesetId { get; set; } = "classic";

    /// <summary>Seed used to generate the puzzle.</summary>
    public ulong Seed { get; set; }

    /// <summary>Generator attempt that produced the puzzle.</summary>
    public int GenerationAttempt { get; set; }

    /// <summary>Givens encoded as 81 digits and dots.</summary>
    public string Givens { get; set; } = string.Empty;

    /// <summary>Canonical solution encoded as 81 digits.</summary>
    public string Solution { get; set; } = string.Empty;

    /// <summary>Current entries encoded as 81 digits and dots.</summary>
    public string Entries { get; set; } = string.Empty;

    /// <summary>Candidate notes as 81 nine-bit masks.</summary>
    public ushort[] Notes { get; set; } = new ushort[81];

    /// <summary>Elapsed play time in seconds.</summary>
    public long ElapsedSeconds { get; set; }

    /// <summary>Incorrect values entered.</summary>
    public int Mistakes { get; set; }

    /// <summary>Hints applied.</summary>
    public int HintsUsed { get; set; }

    /// <summary>Whether the puzzle was complete at save time.</summary>
    public bool Completed { get; set; }

    /// <summary>Measured difficulty band.</summary>
    public SudokuDifficulty Difficulty { get; set; }

    /// <summary>Measured difficulty score.</summary>
    public int DifficultyScore { get; set; }

    /// <summary>Number of givens.</summary>
    public int GivenCount { get; set; }

    /// <summary>Logical steps in the difficulty analysis.</summary>
    public int LogicalSteps { get; set; }

    /// <summary>Search branches in the difficulty analysis.</summary>
    public int SearchNodes { get; set; }

    /// <summary>Backtracks in the difficulty analysis.</summary>
    public int Backtracks { get; set; }

    /// <summary>Maximum search depth in the difficulty analysis.</summary>
    public int MaxSearchDepth { get; set; }

    /// <summary>Whether singles alone solved the puzzle.</summary>
    public bool SolvedByLogic { get; set; }
}
