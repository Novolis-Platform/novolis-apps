using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Cad.Primitives;
using System.Numerics;

namespace DraftStudio.Unit;

public sealed class ToolControllerTests
{
    [Test]
    public async Task Continuous_Line_Chains_Endpoints()
    {
        var (settings, session, bus, dispatcher) = DraftTestHarness.Create();
        settings.Settings.ContinuousLine = true;
        var tools = new CadToolController(dispatcher, settings);
        tools.ContinuousLine = true;
        dispatcher.EnterTool(CadToolKind.Line);

        tools.OnClick(new Vector3(0, 0, 0), 40);
        tools.OnClick(new Vector3(1, 0, 0), 40);
        tools.OnClick(new Vector3(2, 0, 1), 40);

        var lines = session.Document.Entities.Where(e => e.Kind == "line" && (e.Name?.StartsWith("Line") ?? false)).ToList();
        await Assert.That(session.Document.Entities.Count(e => e.Kind == "line")).IsGreaterThanOrEqualTo(3);
        await Assert.That(dispatcher.ActiveTool).IsEqualTo(CadToolKind.Line);
        _ = bus;
        _ = lines;
    }

    [Test]
    public async Task Spline_Close_Near_Start_Commits_Closed()
    {
        var (settings, session, _, dispatcher) = DraftTestHarness.Create();
        var tools = new CadToolController(dispatcher, settings);
        dispatcher.EnterTool(CadToolKind.Spline);
        tools.OnClick(new Vector3(0, 0, 0), 40);
        tools.OnClick(new Vector3(1, 0, 0), 40);
        tools.OnClick(new Vector3(1, 0, 1), 40);
        tools.OnClick(new Vector3(0.01f, 0, 0.01f), 40);

        var spline = session.Document.Entities.LastOrDefault(e => e.Kind == "spline");
        await Assert.That(spline).IsNotNull();
        await Assert.That(spline!.Closed).IsTrue();
    }
}
