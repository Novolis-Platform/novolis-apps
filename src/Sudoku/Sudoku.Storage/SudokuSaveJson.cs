using System.Text.Json;
using System.Text.Json.Serialization;
using Novolis.Game.Sudoku;

namespace Sudoku.Storage;

/// <summary>Versioned JSON codec for import, export, and recovery snapshots.</summary>
public static class SudokuSaveJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Serializes and validates a save record.</summary>
    public static string Serialize(SudokuSaveRecord record, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(record);
        Validate(record);
        var options = indented
            ? Options
            : new JsonSerializerOptions(Options) { WriteIndented = false };
        return JsonSerializer.Serialize(record, options);
    }

    /// <summary>Deserializes and validates a save record.</summary>
    public static SudokuSaveRecord Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var record = JsonSerializer.Deserialize<SudokuSaveRecord>(json, Options)
                     ?? throw new JsonException("The Sudoku save document was empty.");
        Validate(record);
        return record;
    }

    /// <summary>Attempts to deserialize a save record without throwing.</summary>
    public static bool TryDeserialize(
        string? json,
        out SudokuSaveRecord? record,
        out string? error)
    {
        try
        {
            record = Deserialize(json ?? string.Empty);
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            record = null;
            error = exception.Message;
            return false;
        }
        catch (ArgumentException exception)
        {
            record = null;
            error = exception.Message;
            return false;
        }
    }

    private static void Validate(SudokuSaveRecord record)
    {
        if (record.SchemaVersion != SudokuSaveRecord.CurrentSchema)
            throw new JsonException(
                $"Unsupported Sudoku save schema version {record.SchemaVersion}.");
        if (record.Id == Guid.Empty)
            throw new JsonException("The Sudoku save id was empty.");
        if (!string.Equals(record.RulesetId, "classic", StringComparison.OrdinalIgnoreCase))
            throw new JsonException($"Unsupported Sudoku ruleset '{record.RulesetId}'.");
        if (record.Notes is null || record.Notes.Length != SudokuConstants.CellCount)
            throw new JsonException("A Sudoku save must contain 81 note masks.");
        if (record.ElapsedSeconds < 0 || record.Mistakes < 0 || record.HintsUsed < 0)
            throw new JsonException("A Sudoku save contains a negative progress value.");

        SudokuBoard puzzle;
        SudokuBoard solution;
        SudokuBoard entries;
        try
        {
            puzzle = SudokuGridText.Parse(record.Givens);
            solution = SudokuGridText.Parse(record.Solution);
            entries = SudokuGridText.Parse(record.Entries);
        }
        catch (FormatException exception)
        {
            throw new JsonException("The Sudoku save contains an invalid grid.", exception);
        }

        if (!puzzle.Validate().IsValid)
            throw new JsonException("The Sudoku puzzle givens violate classic rules.");
        if (!solution.IsSolved())
            throw new JsonException("The Sudoku solution is not a complete valid grid.");
        for (var cellIndex = 0; cellIndex < SudokuConstants.CellCount; cellIndex++)
        {
            if (puzzle.Get(cellIndex) != SudokuConstants.Empty
                && puzzle.Get(cellIndex) != entries.Get(cellIndex))
            {
                throw new JsonException(
                    $"Saved entries must preserve puzzle given at cell {cellIndex} "
                    + $"({puzzle.Get(cellIndex)} versus {entries.Get(cellIndex)}).");
            }
            if (puzzle.Get(cellIndex) != SudokuConstants.Empty
                && puzzle.Get(cellIndex) != solution.Get(cellIndex))
            {
                throw new JsonException(
                    $"The saved solution changes puzzle given at cell {cellIndex}.");
            }
            _ = new CandidateMask(record.Notes[cellIndex]);
        }
    }

    private static JsonSerializerOptions CreateOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
}
