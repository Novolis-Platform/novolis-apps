namespace Sudoku.Core;

/// <summary>One step in the linear 1–N campaign ordered by difficulty segment.</summary>
public sealed record SudokuLadderPuzzle(
    int Number,
    string PuzzleId,
    SudokuDifficultySegment Segment,
    string SegmentDisplayName,
    int IndexInSegment);
