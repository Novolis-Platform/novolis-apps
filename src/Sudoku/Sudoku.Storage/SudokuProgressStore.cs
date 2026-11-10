using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sudoku.Storage;

/// <summary>Simple file-backed campaign progress under the Sudoku data root.</summary>
public sealed class SudokuProgressStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private SudokuProgressRecord _record;

    /// <summary>Creates a progress store beside the save root.</summary>
    public SudokuProgressStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
        FilePath = Path.Combine(RootPath, "progress.json");
        Directory.CreateDirectory(RootPath);
        _record = LoadOrCreate();
    }

    /// <summary>Data root shared with saves.</summary>
    public string RootPath { get; }

    /// <summary>Progress JSON path.</summary>
    public string FilePath { get; }

    /// <summary>Current in-memory record (do not mutate; use store methods).</summary>
    public SudokuProgressRecord Snapshot
    {
        get
        {
            lock (_gate)
                return Clone(_record);
        }
    }

    /// <summary>True when the dump puzzle id is conquered.</summary>
    public bool IsConquered(string puzzleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puzzleId);
        lock (_gate)
            return _record.ConqueredPuzzleIds.Contains(puzzleId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Records a successful clear and advances unlock.</summary>
    public SudokuProgressRecord RecordCompletion(
        string puzzleId,
        int ladderNumber,
        TimeSpan elapsed,
        int mistakes,
        int hints,
        int ladderCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(puzzleId);
        if (ladderNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(ladderNumber));
        if (ladderCount < 1)
            throw new ArgumentOutOfRangeException(nameof(ladderCount));

        lock (_gate)
        {
            if (!_record.ConqueredPuzzleIds.Contains(puzzleId, StringComparer.OrdinalIgnoreCase))
                _record.ConqueredPuzzleIds.Add(puzzleId);

            var seconds = Math.Max(0, (long)Math.Round(elapsed.TotalSeconds));
            if (!_record.BestTimesSeconds.TryGetValue(puzzleId, out var best) || seconds < best)
                _record.BestTimesSeconds[puzzleId] = seconds;

            _record.Completions++;
            _record.TotalPlaySeconds += seconds;
            _record.TotalMistakes += Math.Max(0, mistakes);
            _record.TotalHints += Math.Max(0, hints);

            var next = Math.Min(ladderCount, ladderNumber + 1);
            _record.HighestUnlockedNumber = Math.Max(_record.HighestUnlockedNumber, next);
            _record.CurrentLadderNumber = Math.Clamp(
                Math.Max(_record.CurrentLadderNumber, next),
                1,
                ladderCount);

            PersistUnlocked();
            return Clone(_record);
        }
    }

    /// <summary>Sets the current open ladder number (must be unlocked).</summary>
    public SudokuProgressRecord SetCurrentLadderNumber(int ladderNumber, int unlockedCeiling)
    {
        if (ladderNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(ladderNumber));
        lock (_gate)
        {
            var max = Math.Max(1, unlockedCeiling);
            _record.CurrentLadderNumber = Math.Clamp(ladderNumber, 1, max);
            PersistUnlocked();
            return Clone(_record);
        }
    }

    /// <summary>Raises the unlock ceiling through the given ladder number (inclusive).</summary>
    public SudokuProgressRecord EnsureUnlockedThrough(int ladderNumber)
    {
        if (ladderNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(ladderNumber));
        lock (_gate)
        {
            if (_record.HighestUnlockedNumber < ladderNumber)
                _record.HighestUnlockedNumber = ladderNumber;
            PersistUnlocked();
            return Clone(_record);
        }
    }

    /// <summary>Adds elapsed play without a completion.</summary>
    public void AddPlaySeconds(long seconds)
    {
        if (seconds <= 0)
            return;
        lock (_gate)
        {
            _record.TotalPlaySeconds += seconds;
            PersistUnlocked();
        }
    }

    private SudokuProgressRecord LoadOrCreate()
    {
        if (!File.Exists(FilePath))
            return new SudokuProgressRecord();

        try
        {
            var json = File.ReadAllText(FilePath);
            var record = JsonSerializer.Deserialize<SudokuProgressRecord>(json, Options)
                ?? new SudokuProgressRecord();
            if (record.SchemaVersion != SudokuProgressRecord.CurrentSchema)
                record.SchemaVersion = SudokuProgressRecord.CurrentSchema;
            Normalize(record);
            return record;
        }
        catch
        {
            return new SudokuProgressRecord();
        }
    }

    private void PersistUnlocked()
    {
        Normalize(_record);
        Directory.CreateDirectory(RootPath);
        var json = JsonSerializer.Serialize(_record, Options);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }

    private static void Normalize(SudokuProgressRecord record)
    {
        record.SchemaVersion = SudokuProgressRecord.CurrentSchema;
        record.CurrentLadderNumber = Math.Max(1, record.CurrentLadderNumber);
        // Puzzle #1 is always open — never allow a zero unlock floor.
        record.HighestUnlockedNumber = Math.Max(1, record.HighestUnlockedNumber);
        if (record.CurrentLadderNumber > record.HighestUnlockedNumber)
            record.CurrentLadderNumber = record.HighestUnlockedNumber;
        record.ConqueredPuzzleIds ??= [];
        record.BestTimesSeconds ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    }

    private static SudokuProgressRecord Clone(SudokuProgressRecord source) =>
        new()
        {
            SchemaVersion = source.SchemaVersion,
            CurrentLadderNumber = source.CurrentLadderNumber,
            HighestUnlockedNumber = source.HighestUnlockedNumber,
            ConqueredPuzzleIds = [.. source.ConqueredPuzzleIds],
            BestTimesSeconds = new Dictionary<string, long>(
                source.BestTimesSeconds,
                StringComparer.OrdinalIgnoreCase),
            Completions = source.Completions,
            TotalPlaySeconds = source.TotalPlaySeconds,
            TotalMistakes = source.TotalMistakes,
            TotalHints = source.TotalHints,
        };
}
