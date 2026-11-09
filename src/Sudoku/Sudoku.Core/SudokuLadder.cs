using System.Security.Cryptography;
using System.Text;

namespace Sudoku.Core;

/// <summary>
/// Flattens the pre-generated catalog into a stable 1–N ladder (Beginner → Expert).
/// Puzzle ids are the durable conquer keys; session Guids are derived from those ids.
/// </summary>
public sealed class SudokuLadder
{
    private readonly IReadOnlyList<SudokuLadderPuzzle> _steps;
    private readonly Dictionary<string, SudokuLadderPuzzle> _byId;

    private SudokuLadder(IReadOnlyList<SudokuLadderPuzzle> steps)
    {
        _steps = steps;
        _byId = steps.ToDictionary(
            step => step.PuzzleId,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Builds a ladder from catalog segment order.</summary>
    public static SudokuLadder FromCatalog(SudokuPuzzleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var steps = new List<SudokuLadderPuzzle>(69);
        var number = 1;
        foreach (var segment in catalog.Segments)
        {
            for (var index = 0; index < segment.Games.Count; index++)
            {
                steps.Add(new SudokuLadderPuzzle(
                    number,
                    segment.Games[index].Id,
                    segment.Segment,
                    segment.DisplayName,
                    index));
                number++;
            }
        }

        if (steps.Count == 0)
            throw new InvalidDataException("Sudoku ladder requires at least one puzzle.");
        return new SudokuLadder(steps);
    }

    /// <summary>Total puzzles in the campaign.</summary>
    public int Count => _steps.Count;

    /// <summary>All ladder steps in play order.</summary>
    public IReadOnlyList<SudokuLadderPuzzle> Steps => _steps;

    /// <summary>Returns the 1-based ladder step.</summary>
    public SudokuLadderPuzzle Get(int number)
    {
        if (number < 1 || number > _steps.Count)
            throw new ArgumentOutOfRangeException(nameof(number));
        return _steps[number - 1];
    }

    /// <summary>Finds a step by dump puzzle id.</summary>
    public bool TryGetByPuzzleId(string puzzleId, out SudokuLadderPuzzle? step)
    {
        if (string.IsNullOrWhiteSpace(puzzleId))
        {
            step = null;
            return false;
        }

        return _byId.TryGetValue(puzzleId.Trim(), out step);
    }

    /// <summary>
    /// Deterministic session Guid for a dump puzzle id so conquered / in-progress
    /// saves stay attached to the same ladder step across launches.
    /// </summary>
    public static Guid StableSessionId(string puzzleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puzzleId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("novolis-sudoku:" + puzzleId.Trim()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        // RFC 4122 variant bits for a name-derived Guid.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
