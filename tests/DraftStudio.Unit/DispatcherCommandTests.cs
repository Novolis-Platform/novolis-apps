using Novolis.Avalonia.Cad.Services;
using Novolis.Cad.Primitives;

namespace DraftStudio.Unit;

public sealed class DispatcherCommandTests
{
    [Test]
    [Arguments("Line(0,0,2,0)")]
    [Arguments("Circle(0,0,1.5)")]
    [Arguments("Rect(0,0,2,3)")]
    [Arguments("Spline(0,0,1,1,2,0,3,1)")]
    [Arguments("Box(2,1,4)")]
    [Arguments("Box(1,2,3,2,2,2)")]
    [Arguments("Cylinder(1,2)")]
    [Arguments("Sphere(1.5)")]
    [Arguments("Level(3)")]
    [Arguments("Fit")]
    [Arguments("Undo")]
    [Arguments("Redo")]
    public async Task Dispatch_Known_Commands_Succeed(string prompt)
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        var before = session.Document.Entities.Count;
        var err = dispatcher.TryDispatch(prompt);
        await Assert.That(err).IsNull();

        if (prompt.StartsWith("Line", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Circle", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Rect", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Spline", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Box", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase)
            || prompt.StartsWith("Sphere", StringComparison.OrdinalIgnoreCase))
        {
            await Assert.That(session.Document.Entities.Count).IsGreaterThan(before);
        }
    }

    [Test]
    public async Task Level_Sets_DrawElevation()
    {
        var (settings, _, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Level(4.5)");
        await Assert.That(settings.Settings.DrawElevation).IsEqualTo(4.5f);
    }

    [Test]
    public async Task Line_Uses_Current_Elevation()
    {
        var (settings, session, _, dispatcher) = DraftTestHarness.Create();
        settings.Settings.DrawElevation = 2.5f;
        DraftTestHarness.DispatchOk(dispatcher, "Line(0,0,1,0)");
        var line = session.Document.Entities.Last(e => e.Kind == "line");
        await Assert.That(CadVec.To(line.A).Y).IsEqualTo(2.5f);
        await Assert.That(CadVec.To(line.B).Y).IsEqualTo(2.5f);
    }

    [Test]
    public async Task Move_Requires_Selection()
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        session.SelectedId = null;
        var err = dispatcher.TryDispatch("Move(1,0,0)");
        await Assert.That(err).IsNotNull();
    }

    [Test]
    public async Task Move_Translates_Selected()
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Line(0,0,1,0)");
        var line = session.Document.Entities.Last(e => e.Kind == "line");
        session.SelectedId = line.Id;
        DraftTestHarness.DispatchOk(dispatcher, "Move(2,0,0)");
        await Assert.That(CadVec.To(line.A).X).IsEqualTo(2f);
    }

    [Test]
    public async Task Delete_Removes_Selected()
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Circle(0,0,1)");
        var id = session.SelectedId;
        await Assert.That(id).IsNotNull();
        DraftTestHarness.DispatchOk(dispatcher, "Delete");
        await Assert.That(session.Document.Entities.Any(e => e.Id == id)).IsFalse();
    }

    [Test]
    public async Task Enter_Tool_Without_Args()
    {
        var (_, _, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Line");
        await Assert.That(dispatcher.ActiveTool.ToString()).IsEqualTo("Line");
        DraftTestHarness.DispatchOk(dispatcher, "Select");
        await Assert.That(dispatcher.ActiveTool.ToString()).IsEqualTo("Select");
    }

    [Test]
    public async Task Unknown_Command_Fails()
    {
        var (_, _, _, dispatcher) = DraftTestHarness.Create();
        var err = dispatcher.TryDispatch("Teleport(1,2,3)");
        await Assert.That(err).IsNotNull();
    }

    [Test]
    public async Task Save_And_Dump_Commands_Are_Recognized()
    {
        var (_, _, _, dispatcher) = DraftTestHarness.Create();
        var saveHits = 0;
        var dumpHits = 0;
        dispatcher.SaveRequested += () => saveHits++;
        dispatcher.DumpArtifactsRequested += () => dumpHits++;
        DraftTestHarness.DispatchOk(dispatcher, "Save");
        DraftTestHarness.DispatchOk(dispatcher, "Dump");
        DraftTestHarness.DispatchOk(dispatcher, "DumpModel");
        await Assert.That(saveHits).IsEqualTo(1);
        await Assert.That(dumpHits).IsEqualTo(2);
    }
}
