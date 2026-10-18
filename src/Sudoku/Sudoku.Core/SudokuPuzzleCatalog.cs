using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Game.Sudoku;

namespace Sudoku.Core;

/// <summary>Loads and serves the fixed initial library of Sudoku games.</summary>
public sealed class SudokuPuzzleCatalog
{
    private const int CurrentSchemaVersion = 1;
    private readonly SudokuDifficultyAnalyzer _analyzer = new();
    private readonly IReadOnlyDictionary<SudokuDifficultySegment, SudokuPuzzleSegment> _bySegment;

    private SudokuPuzzleCatalog(IReadOnlyList<SudokuPuzzleSegment> segments)
    {
        Segments = segments;
        _bySegment = segments.ToDictionary(segment => segment.Segment);
    }

    /// <summary>All five named segments in display order.</summary>
    public IReadOnlyList<SudokuPuzzleSegment> Segments { get; }

    /// <summary>Loads the embedded 69-game dump.</summary>
    public static SudokuPuzzleCatalog LoadDefault()
    {
        var assembly = typeof(SudokuPuzzleCatalog).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(name =>
                name.EndsWith(
                    "Assets.SudokuPuzzleDump.json",
                    StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException("The embedded Sudoku puzzle dump is missing.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded Sudoku puzzle dump could not be opened.");
        return Load(stream);
    }

    /// <summary>Loads and validates a dump stream.</summary>
    public static SudokuPuzzleCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var dump = JsonSerializer.Deserialize<DumpDocument>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Converters = { new JsonStringEnumConverter() },
            })
            ?? throw new InvalidDataException("The Sudoku puzzle dump is empty.");

        if (dump.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported Sudoku puzzle dump schema {dump.SchemaVersion}.");
        if (dump.Segments.Count != 5)
            throw new InvalidDataException("The Sudoku puzzle dump must contain five segments.");

        var segments = dump.Segments
            .Select(segment => new SudokuPuzzleSegment
            {
                Key = segment.Key,
                DisplayName = segment.DisplayName,
                EngineDifficulty = segment.EngineDifficulty,
                Games = segment.Games
                    .Select(game => new SudokuPuzzleDefinition
                    {
                        Id = game.Id,
                        Seed = game.Seed,
                        Puzzle = game.Puzzle,
                        Solution = game.Solution,
                    })
                    .ToArray(),
            })
            .ToArray();

        var allGames = segments.SelectMany(segment => segment.Games).ToArray();
        if (allGames.Length != 69)
            throw new InvalidDataException(
                $"The Sudoku puzzle dump must contain 69 games, not {allGames.Length}.");
        if (allGames.Any(game => string.IsNullOrWhiteSpace(game.Id)))
            throw new InvalidDataException("Every dumped Sudoku game needs an id.");
        if (allGames.Select(game => game.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != allGames.Length)
        {
            throw new InvalidDataException("Sudoku dump game ids must be unique.");
        }

        return new SudokuPuzzleCatalog(segments);
    }

    /// <summary>Returns a named segment.</summary>
    public SudokuPuzzleSegment GetSegment(SudokuDifficultySegment segment) =>
        _bySegment.TryGetValue(segment, out var result)
            ? result
            : throw new ArgumentOutOfRangeException(nameof(segment));

    /// <summary>Creates a game from a segment and wrapping puzzle index.</summary>
    public SudokuGeneratedGame CreateGame(
        SudokuDifficultySegment segment,
        int index)
    {
        var selected = GetSegment(segment);
        if (selected.Games.Count == 0)
            throw new InvalidDataException($"Sudoku segment '{selected.Key}' is empty.");
        var definition = selected.Games[index % selected.Games.Count];
        return definition.CreateGame(_analyzer);
    }

    /// <summary>Finds the dump entry that owns a puzzle, if present.</summary>
    public bool TryFind(
        SudokuBoard puzzle,
        out SudokuPuzzleSegment? segment,
        out SudokuPuzzleDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(puzzle);
        var serialized = SudokuGridText.Serialize(puzzle);
        foreach (var candidateSegment in Segments)
        {
            definition = candidateSegment.Games.FirstOrDefault(
                game => string.Equals(game.Puzzle, serialized, StringComparison.Ordinal));
            if (definition is not null)
            {
                segment = candidateSegment;
                return true;
            }
        }

        segment = null;
        definition = null;
        return false;
    }

    private sealed class DumpDocument
    {
        public int SchemaVersion { get; set; }

        public List<DumpSegment> Segments { get; set; } = [];
    }

    private sealed class DumpSegment
    {
        public string Key { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public SudokuDifficulty EngineDifficulty { get; set; }

        public List<DumpGame> Games { get; set; } = [];
    }

    private sealed class DumpGame
    {
        public string Id { get; set; } = string.Empty;

        public ulong Seed { get; set; }

        public string Puzzle { get; set; } = string.Empty;

        public string Solution { get; set; } = string.Empty;
    }
}
