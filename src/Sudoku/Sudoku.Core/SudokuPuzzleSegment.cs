using Novolis.Game.Sudoku;

namespace Sudoku.Core;

/// <summary>A named difficulty segment from the pre-generated puzzle library.</summary>
public sealed record SudokuPuzzleSegment
{
    /// <summary>Stable key used by the dump.</summary>
    public required string Key { get; init; }

    /// <summary>Player-facing difficulty name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Engine difficulty band used when the segment was generated.</summary>
    public required SudokuDifficulty EngineDifficulty { get; init; }

    /// <summary>Pre-generated games in this segment.</summary>
    public required IReadOnlyList<SudokuPuzzleDefinition> Games { get; init; }

    /// <summary>Player-facing segment identifier.</summary>
    public SudokuDifficultySegment Segment =>
        Enum.Parse<SudokuDifficultySegment>(Key, ignoreCase: true);
}
