using Novolis.Avalonia.Cad.Services;
using Novolis.Cad.Primitives;

namespace DraftStudio.Unit;

public sealed class EntityBoundsTests
{
    [Test]
    public async Task Compute_Includes_Line_Extent()
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        session.Document.Entities.Clear();
        DraftTestHarness.DispatchOk(dispatcher, "Line(-10,0,10,0)");
        var (center, radius) = EntityBounds.Compute(session.Document);
        await Assert.That(radius).IsGreaterThanOrEqualTo(10f);
        await Assert.That(Math.Abs(center.X)).IsLessThan(0.1f);
    }

    [Test]
    public async Task Compute_Empty_Returns_Default_Radius()
    {
        var doc = new CadDocument();
        var (center, radius) = EntityBounds.Compute(doc);
        await Assert.That(center).IsEqualTo(System.Numerics.Vector3.Zero);
        await Assert.That(radius).IsEqualTo(5f);
    }
}
