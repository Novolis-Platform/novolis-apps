using Novolis.Game.Sudoku;

namespace Sudoku.Core;

/// <summary>One pre-generated puzzle and its canonical solution.</summary>
public sealed record SudokuPuzzleDefinition
{
    /// <summary>Stable identifier within the dump.</summary>
    public required string Id { get; init; }

    /// <summary>Seed retained for provenance and display.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Compact puzzle grid using digits and dots.</summary>
    public required string Puzzle { get; init; }

    /// <summary>Compact complete solution grid using digits.</summary>
    public required string Solution { get; init; }

    /// <summary>Builds the host-neutral game object from the dump entry.</summary>
    public SudokuGeneratedGame CreateGame(SudokuDifficultyAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        var puzzle = SudokuGridText.Parse(Puzzle);
        var solution = SudokuGridText.Parse(Solution);
        var report = analyzer.Analyze(puzzle);

        if (!solution.IsSolved(ClassicSudokuRuleSet.Default))
            throw new InvalidDataException($"Dump solution '{Id}' is not a solved classic grid.");

        if (!report.IsValidAndUnique)
            throw new InvalidDataException($"Dump puzzle '{Id}' is not uniquely solvable.");

        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            if (puzzle.Get(cellIndex) != SudokuConstants.Empty
                && puzzle.Get(cellIndex) != solution.Get(cellIndex))
            {
                throw new InvalidDataException(
                    $"Dump puzzle '{Id}' disagrees with its solution at cell {cellIndex}.");
            }
        }

        return new SudokuGeneratedGame
        {
            Seed = Seed,
            Puzzle = puzzle,
            Solution = solution,
            Difficulty = report,
            GenerationAttempt = 0,
        };
    }
}
