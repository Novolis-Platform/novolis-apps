using Microsoft.Extensions.DependencyInjection;
using Novolis.Game.Sudoku;
using Novolis.IO.Recovery;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Sudoku.Core;

namespace Sudoku.Storage;

/// <summary>
/// Local file-backed save facade. Normal saves use the JSON repository; crash
/// recovery snapshots use the separate IO recovery store.
/// </summary>
public sealed class SudokuSaveStore : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IRepository<SudokuSaveRecord> _repository;
    private readonly ContentRecoveryStore _recovery;
    private bool _disposed;

    /// <summary>Creates a store under the application data directory.</summary>
    public SudokuSaveStore(string? rootPath = null)
    {
        RootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Novolis",
                "sudoku")
            : Path.GetFullPath(rootPath);
        SaveRootPath = Path.Combine(RootPath, "saves");
        RecoveryRootPath = Path.Combine(RootPath, "recovery");
        Directory.CreateDirectory(RootPath);

        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddJsonProvider(options =>
        {
            options.RootPath = SaveRootPath;
            options.UseProcessLock = false;
        }));
        _provider = services.BuildServiceProvider();
        _repository = _provider.GetRequiredService<IRepository<SudokuSaveRecord>>();
        _recovery = new ContentRecoveryStore(RecoveryRootPath, maxSnapshotsPerDocument: 8);
    }

    /// <summary>Root directory for Sudoku data.</summary>
    public string RootPath { get; }

    /// <summary>Normal save root.</summary>
    public string SaveRootPath { get; }

    /// <summary>Recovery snapshot root.</summary>
    public string RecoveryRootPath { get; }

    /// <summary>Lists valid save records newest first.</summary>
    public IReadOnlyList<SudokuSaveRecord> List() =>
        _repository.All()
            .OrderByDescending(record => record.SavedUtc)
            .ToArray();

    /// <summary>Returns a save by id.</summary>
    public ValueTask<SudokuSaveRecord?> TryGetAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _repository.TryGetAsync(id, cancellationToken);

    /// <summary>Loads and reconstructs a player session.</summary>
    public async ValueTask<SudokuSession?> LoadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await TryGetAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
            return null;
        _ = SudokuSaveJson.Serialize(record, indented: false);
        return ToSession(record);
    }

    /// <summary>Returns the newest save as a reconstructed session.</summary>
    public async ValueTask<SudokuSession?> LoadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        var latest = List().FirstOrDefault();
        return latest is null ? null : await LoadAsync(latest.Id, cancellationToken);
    }

    /// <summary>Saves the current puzzle, solution, and progress atomically.</summary>
    public async ValueTask<SudokuSaveRecord> SaveAsync(
        SudokuSession session,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var record = FromSession(session, label);
        _ = SudokuSaveJson.Serialize(record, indented: false);
        await _repository.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        _recovery.Clear(session.Id.ToString("N"));
        return record;
    }

    /// <summary>Deletes a normal save.</summary>
    public ValueTask<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    /// <summary>Writes an autosave snapshot without replacing the named save.</summary>
    public void WriteRecovery(SudokuSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var record = FromSession(session, label: "recovery");
        _recovery.WriteSnapshot(
            session.Id.ToString("N"),
            SudokuSaveJson.Serialize(record));
    }

    /// <summary>Returns the latest recovery snapshot for a session.</summary>
    public RecoverySnapshotInfo? GetLatestRecovery(Guid id) =>
        _recovery.GetLatest(id.ToString("N"));

    /// <summary>Removes all recovery snapshots for a session.</summary>
    public void ClearRecovery(Guid id) => _recovery.Clear(id.ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _provider.Dispose();
    }

    private static SudokuSaveRecord FromSession(SudokuSession session, string? label)
    {
        var report = session.Game.Difficulty;
        return new SudokuSaveRecord
        {
            Id = session.Id,
            SchemaVersion = SudokuSaveRecord.CurrentSchema,
            Label = string.IsNullOrWhiteSpace(label)
                ? $"{report.Difficulty} · seed {session.Game.Seed}"
                : label.Trim(),
            SavedUtc = DateTimeOffset.UtcNow,
            RulesetId = "classic",
            Seed = session.Game.Seed,
            GenerationAttempt = session.Game.GenerationAttempt,
            Givens = SudokuGridText.Serialize(session.Puzzle),
            Solution = SudokuGridText.Serialize(session.Solution),
            Entries = SudokuGridText.Serialize(session.Entries),
            Notes = session.Notes.Select(note => note.Bits).ToArray(),
            ElapsedSeconds = Math.Max(0, (long)Math.Round(session.Elapsed.TotalSeconds)),
            Mistakes = session.Mistakes,
            HintsUsed = session.HintsUsed,
            Completed = session.IsCompleted,
            Difficulty = report.Difficulty,
            DifficultyScore = report.Score,
            GivenCount = report.GivenCount,
            LogicalSteps = report.LogicalSteps,
            SearchNodes = report.SearchNodes,
            Backtracks = report.Backtracks,
            MaxSearchDepth = report.MaxSearchDepth,
            SolvedByLogic = report.SolvedByLogic,
        };
    }

    private static SudokuSession ToSession(SudokuSaveRecord record)
    {
        if (!string.Equals(record.RulesetId, "classic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported Sudoku ruleset '{record.RulesetId}'.");

        var puzzle = SudokuGridText.Parse(record.Givens);
        var solution = SudokuGridText.Parse(record.Solution);
        var entries = SudokuGridText.Parse(record.Entries);
        var report = new SudokuDifficultyReport
        {
            IsValidAndUnique = true,
            Difficulty = record.Difficulty,
            Score = record.DifficultyScore,
            GivenCount = record.GivenCount,
            LogicalSteps = record.LogicalSteps,
            SearchNodes = record.SearchNodes,
            Backtracks = record.Backtracks,
            MaxSearchDepth = record.MaxSearchDepth,
            HighestTechnique = record.SolvedByLogic
                ? SudokuTechnique.HiddenSingle
                : SudokuTechnique.Guess,
            SolvedByLogic = record.SolvedByLogic,
        };
        var game = new SudokuGeneratedGame
        {
            Seed = record.Seed,
            Puzzle = puzzle,
            Solution = solution,
            Difficulty = report,
            GenerationAttempt = record.GenerationAttempt,
        };
        return SudokuSession.Restore(
            record.Id,
            game,
            entries,
            record.Notes.Select(bits => new CandidateMask(bits)),
            TimeSpan.FromSeconds(record.ElapsedSeconds),
            record.Mistakes,
            record.HintsUsed,
            record.Completed);
    }
}
