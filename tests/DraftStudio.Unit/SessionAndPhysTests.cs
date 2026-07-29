using System.Text.Json;
using DraftStudio.Models;
using DraftStudio.Services;

namespace DraftStudio.Unit;

public sealed class SessionAndPhysTests
{
    [Test]
    public async Task Save_And_Reload_Preserves_Entities()
    {
        var (settings, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Line(0,0,5,0)");
        DraftTestHarness.DispatchOk(dispatcher, "Spline(0,0,1,1,2,0)");
        DraftTestHarness.DispatchOk(dispatcher, "Box(1,1,1)");
        session.Save();
        var count = session.Document.Entities.Count;
        var path = session.DocumentPath;

        var session2 = new DraftStudio.Core.DraftSession(settings);
        session2.OpenFromPath(path);
        await Assert.That(session2.Document.Entities.Count).IsEqualTo(count);
        await Assert.That(session2.Document.Format).IsEqualTo("novolis.cad");
        await Assert.That(session2.Document.Entities.Any(e => e.Kind == "spline")).IsTrue();
    }

    [Test]
    public async Task Cadjson_On_Disk_Has_Required_Header()
    {
        var (_, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Circle(0,0,2)");
        session.Save();
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(session.DocumentPath));
        await Assert.That(doc.RootElement.GetProperty("format").GetString()).IsEqualTo("novolis.cad");
        await Assert.That(doc.RootElement.GetProperty("schemaVersion").GetInt32()).IsEqualTo(1);
        await Assert.That(doc.RootElement.GetProperty("linearUnit").GetString()).IsEqualTo("meter");
        await Assert.That(doc.RootElement.GetProperty("entities").GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    public async Task Phys_Export_Has_Meshes_And_Colliders()
    {
        var (settings, session, _, dispatcher) = DraftTestHarness.Create();
        DraftTestHarness.DispatchOk(dispatcher, "Box(2,1,3)");
        DraftTestHarness.DispatchOk(dispatcher, "Sphere(1)");
        var exporter = new CadPhysExporter();
        var phys = exporter.Build(session.Document);
        exporter.Write(phys, settings.PhysDocumentPath);
        await Assert.That(File.Exists(settings.PhysDocumentPath)).IsTrue();
        await Assert.That(phys.Meshes.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(phys.Colliders.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(phys.Meshes.All(m => m.Indices.Count % 3 == 0)).IsTrue();
    }

    [Test]
    public async Task NewDocument_Is_Dirty_Starter()
    {
        var (_, session, _, _) = DraftTestHarness.Create();
        session.NewDocument();
        await Assert.That(session.IsDirty).IsTrue();
        await Assert.That(session.Document.Entities.Count).IsGreaterThanOrEqualTo(2);
    }
}
