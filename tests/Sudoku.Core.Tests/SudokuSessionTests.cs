using Novolis.Game.Sudoku;
using Sudoku.Core;

namespace Sudoku.Core.Tests;

public sealed class SudokuSessionTests
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
    public async Task Session_Preserves_Givens_And_Supports_Undo_Redo()
    {
        var session = SudokuSession.Create(Guid.CreateVersion7(), CreateGame());

        await Assert.That(session.IsGiven(0)).IsTrue();
        await Assert.That(session.TrySetValue(2, 4, out _)).IsTrue();
        await Assert.That(session.Entries.Get(2)).IsEqualTo((byte)4);
        await Assert.That(session.Undo()).IsTrue();
        await Assert.That(session.Entries.Get(2)).IsEqualTo((byte)0);
        await Assert.That(session.Redo()).IsTrue();
        await Assert.That(session.Entries.Get(2)).IsEqualTo((byte)4);
        await Assert.That(session.TrySetValue(0, 1, out var givenMessage)).IsFalse();
        await Assert.That(givenMessage).Contains("Given");
    }

    [Test]
    public async Task Session_Tracks_Notes_Mistakes_And_Hints()
    {
        var session = SudokuSession.Create(Guid.CreateVersion7(), CreateGame());

        await Assert.That(session.ToggleNote(2, 4)).IsTrue();
        await Assert.That(session.GetNotes(2).Contains(4)).IsTrue();
        await Assert.That(session.ToggleNote(2, 4)).IsTrue();
        await Assert.That(session.GetNotes(2).Contains(4)).IsFalse();
        await Assert.That(session.TrySetValue(2, 1, out _)).IsTrue();
        await Assert.That(session.Mistakes).IsEqualTo(1);

        var hint = session.FindHint();
        await Assert.That(hint).IsNotNull();
        await Assert.That(hint!.Value).IsEqualTo(session.Solution.Get(hint.CellIndex));
        await Assert.That(session.ApplyHint()).IsNotNull();
        await Assert.That(session.HintsUsed).IsEqualTo(1);
    }

    [Test]
    public async Task Restore_Rejects_Entries_That_Change_Givens()
    {
        var game = CreateGame();
        var entries = game.Puzzle.Clone();
        entries.Set(0, 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            _ = SudokuSession.Restore(
                Guid.CreateVersion7(),
                game,
                entries,
                new CandidateMask[SudokuConstants.CellCount],
                TimeSpan.Zero,
                mistakes: 0,
                hintsUsed: 0,
                completed: false);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task DefaultCatalog_Contains_SixtyNine_Games_In_Five_Difficulty_Segments()
    {
        var catalog = SudokuPuzzleCatalog.LoadDefault();

        await Assert.That(catalog.Segments.Count).IsEqualTo(5);
        await Assert.That(catalog.Segments.SelectMany(segment => segment.Games).Count())
            .IsEqualTo(69);
        await Assert.That(catalog.GetSegment(SudokuDifficultySegment.Beginner).Games.Count)
            .IsEqualTo(14);
        await Assert.That(catalog.GetSegment(SudokuDifficultySegment.Easy).Games.Count)
            .IsEqualTo(14);
        await Assert.That(catalog.GetSegment(SudokuDifficultySegment.Medium).Games.Count)
            .IsEqualTo(14);
        await Assert.That(catalog.GetSegment(SudokuDifficultySegment.Hard).Games.Count)
            .IsEqualTo(14);
        await Assert.That(catalog.GetSegment(SudokuDifficultySegment.Expert).Games.Count)
            .IsEqualTo(13);

        var analyzer = new SudokuDifficultyAnalyzer();
        foreach (var definition in catalog.Segments.SelectMany(segment => segment.Games))
        {
            var game = definition.CreateGame(analyzer);
            await Assert.That(game.Difficulty.IsValidAndUnique).IsTrue();
        }
    }

    private static SudokuGeneratedGame CreateGame() =>
        new()
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
}
