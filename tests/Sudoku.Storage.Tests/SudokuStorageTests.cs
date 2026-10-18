using System.Text.Json;
using Novolis.Game.Sudoku;
using Sudoku.Core;
using Sudoku.Storage;

namespace Sudoku.Storage.Tests;

public sealed class SudokuStorageTests
{
    private const string Puzzle =
        "530070000"
        + "600195000"
        + "098000060"
        + "800060003"
        + "400803001"
        + "700020006"
        + "060000280"
        + "000419005"
        + "000080079";

    private const string Solution =
        "534678912"
        + "672195348"
        + "198342567"
        + "859761423"
        + "426853791"
        + "713924856"
        + "961537284"
        + "287419635"
        + "345286179";

    [Test]
    public async Task Save_And_Load_Preserve_Progress_And_Notes()
    {
        var root = CreateRoot();
        try
        {
            using var store = new SudokuSaveStore(root);
            var session = CreateSession();
            session.TrySetValue(2, 1, out _);
            session.ToggleNote(3, 4);

            var saved = await store.SaveAsync(session);
            var loaded = await store.LoadAsync(saved.Id);

            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.Id).IsEqualTo(session.Id);
            await Assert.That(loaded.Entries.Get(2)).IsEqualTo((byte)1);
            await Assert.That(loaded.Mistakes).IsEqualTo(1);
            await Assert.That(loaded.GetNotes(3).Contains(4)).IsTrue();
            await Assert.That(store.List().Count).IsEqualTo(1);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public async Task Recovery_Snapshot_Is_Separate_And_Versioned()
    {
        var root = CreateRoot();
        try
        {
            using var store = new SudokuSaveStore(root);
            var session = CreateSession();
            store.WriteRecovery(session);

            var recovery = store.GetLatestRecovery(session.Id);
            await Assert.That(recovery).IsNotNull();
            await Assert.That(recovery!.Content).Contains("\"schemaVersion\": 1");
            var decoded = SudokuSaveJson.Deserialize(recovery.Content);
            await Assert.That(decoded.Id).IsEqualTo(session.Id);
            await Assert.That(store.List().Count).IsEqualTo(0);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public async Task Json_Codec_Rejects_Unknown_Schema()
    {
        var record = new SudokuSaveRecord
        {
            Id = Guid.CreateVersion7(),
            SchemaVersion = SudokuSaveRecord.CurrentSchema + 1,
            Notes = new ushort[SudokuConstants.CellCount],
        };

        await Assert.ThrowsAsync<JsonException>(() =>
        {
            _ = SudokuSaveJson.Serialize(record);
            return Task.CompletedTask;
        });
    }

    private static SudokuSession CreateSession()
    {
        var game = new SudokuGeneratedGame
        {
            Seed = 7,
            Puzzle = SudokuGridText.Parse(Puzzle),
            Solution = SudokuGridText.Parse(Solution),
            Difficulty = new SudokuDifficultyReport
            {
                IsValidAndUnique = true,
                Difficulty = SudokuDifficulty.Medium,
                Score = 200,
                GivenCount = 30,
            },
        };
        return SudokuSession.Create(Guid.CreateVersion7(), game);
    }

    private static string CreateRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            "novolis-sudoku-tests",
            Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
