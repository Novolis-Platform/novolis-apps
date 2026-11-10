namespace Sudoku.App;

/// <summary>Home → play navigation payload.</summary>
public readonly record struct SudokuPlayRequest(SudokuPlayKind Kind, int LadderNumber = 0)
{
    /// <summary>Continue from save / campaign point.</summary>
    public static SudokuPlayRequest Continue() => new(SudokuPlayKind.Continue);

    /// <summary>Start a specific unlocked ladder step.</summary>
    public static SudokuPlayRequest Ladder(int number) => new(SudokuPlayKind.Ladder, number);
}
