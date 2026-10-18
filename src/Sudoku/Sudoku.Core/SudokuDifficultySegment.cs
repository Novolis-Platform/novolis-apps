namespace Sudoku.Core;

/// <summary>One of the five pre-generated player-facing puzzle segments.</summary>
public enum SudokuDifficultySegment
{
    /// <summary>Gentle introductions using the easiest puzzle band.</summary>
    Beginner,

    /// <summary>Easy puzzles for relaxed play.</summary>
    Easy,

    /// <summary>Balanced everyday puzzles.</summary>
    Medium,

    /// <summary>Puzzles requiring sustained deduction.</summary>
    Hard,

    /// <summary>The most demanding puzzles in the initial library.</summary>
    Expert,
}
