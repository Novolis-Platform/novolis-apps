namespace Sudoku.App;

/// <summary>How the player enters the board from home.</summary>
public enum SudokuPlayKind
{
    /// <summary>Resume latest save, else continue campaign point.</summary>
    Continue,

    /// <summary>Open a specific unlocked ladder number.</summary>
    Ladder,
}
