using DraftStudio.Commands;
using DraftStudio.Models;

namespace DraftStudio.Unit;

public sealed class GeometryEditTests
{
    [Test]
    public async Task MutateEntityGeometry_Undo_Restores()
    {
        var (_, session, bus, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Line(0,0,2,0)");
        var line = session.Document.Entities.Last(e => e.Kind == "line");
        var before = EntityGeometrySnapshot.Capture(line);
        line.B = CadVec.Plan(5, 0, CadVec.To(line.B).Y);
        var after = EntityGeometrySnapshot.Capture(line);
        bus.Execute(new MutateEntityGeometryCommand(line.Id, before, after));
        await Assert.That(CadVec.To(line.B).X).IsEqualTo(5f);
        bus.Undo();
        await Assert.That(CadVec.To(line.B).X).IsEqualTo(2f);
        bus.Redo();
        await Assert.That(CadVec.To(line.B).X).IsEqualTo(5f);
    }

    [Test]
    public async Task Box_HalfExtents_Snapshot_RoundTrip()
    {
        var box = new CadEntity
        {
            Kind = "box",
            Center = CadVec.Xyz(0, 0.5f, 0),
            HalfExtents = [1, 0.5f, 2],
        };
        var snap = EntityGeometrySnapshot.Capture(box);
        box.HalfExtents = [9, 9, 9];
        snap.ApplyTo(box);
        await Assert.That(box.HalfExtents![0]).IsEqualTo(1f);
        await Assert.That(box.HalfExtents[2]).IsEqualTo(2f);
    }
}
