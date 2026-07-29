using System.Globalization;

namespace DraftStudio.Unit;

/// <summary>Large corpus of DSL prompts — parse + dispatch smoke for regressions.</summary>
public sealed class CommandCorpusTests
{
    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    public static IEnumerable<string> GeometryCorpus()
    {
        for (var i = 0; i < 40; i++)
        {
            var x = i * 0.5;
            yield return $"Line({F(x)},0,{F(x + 1)},1)";
            yield return $"Circle({F(x)},0,{F(0.25 + i * 0.01)})";
            yield return $"Rect({F(x)},0,{F(x + 0.8)},{F(0.8)})";
        }

        for (var i = 0; i < 20; i++)
            yield return $"Box({F(1 + i * 0.1)},1,{F(1 + i * 0.05)})";

        yield return "Spline(0,0,1,1,2,0,3,1,4,0)";
        yield return "Spline(-2,-2,-1,0,0,1,1,0,2,-1)";
        yield return "Cylinder(0.5,2)";
        yield return "Sphere(0.75)";
        yield return "Level(0)";
        yield return "Level(3)";
        yield return "Level(-1.5)";
    }

    [Test]
    [MethodDataSource(nameof(GeometryCorpus))]
    public async Task Corpus_Dispatches_Without_Error(string prompt)
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        var err = dispatcher.TryDispatch(prompt);
        await Assert.That(err).IsNull();
        await Assert.That(session.Document.Entities.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task House_Floor_Script_Builds_Two_Levels()
    {
        var (settings, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Level(0)");
        DraftTestHarness.DispatchOk(dispatcher, "Rect(0,0,10,8)");
        DraftTestHarness.DispatchOk(dispatcher, "Rect(1,1,3,2)"); // room
        DraftTestHarness.DispatchOk(dispatcher, "Level(3)");
        DraftTestHarness.DispatchOk(dispatcher, "Rect(0,0,10,8)");
        DraftTestHarness.DispatchOk(dispatcher, "Circle(5,4,1)");
        session.Save();

        var ground = session.Document.Entities.Count(e =>
            CadVecElevation(e) is >= -0.01f and <= 0.01f && e.Kind is "rect" or "circle");
        var upper = session.Document.Entities.Count(e =>
            CadVecElevation(e) is >= 2.9f and <= 3.1f && e.Kind is "rect" or "circle");

        await Assert.That(ground).IsGreaterThanOrEqualTo(2);
        await Assert.That(upper).IsGreaterThanOrEqualTo(2);
        await Assert.That(File.Exists(session.DocumentPath)).IsTrue();
        await Assert.That(settings.Settings.DrawElevation).IsEqualTo(3f);
    }

    private static float CadVecElevation(DraftStudio.Models.CadEntity e) =>
        DraftStudio.Models.CadVec.ElevationOf(e);
}
