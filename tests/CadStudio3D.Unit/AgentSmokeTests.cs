using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia._3D.Session;
using Novolis._3D;

namespace CadStudio3D.Unit;

public sealed class AgentSmokeTests
{
    [Test]
    public async Task SmokeRunner_ExitsZero()
    {
        var code = CadStudio3D.SmokeRunner.Run();
        await Assert.That(code).IsEqualTo(0);
    }

    [Test]
    public async Task Catalogs_ContainParityAllowlist()
    {
        var settings = new CadEditorSettings(Path.Combine(Path.GetTempPath(), "cad3d-unit-" + Guid.NewGuid().ToString("N")));
        var document = new CadDocumentSession(settings);
        var bus = new CadCommandBus(document);
        var dispatcher = new CadCommandDispatcher(document, bus, settings);
        var cad = new CadSessionService(document, settings, bus, dispatcher);
        var scene = new SceneSessionService();

        var cadIds = cad.Actions().Actions.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in new[]
                 {
                     CadSessionActionIds.ExportScene,
                     CadSessionActionIds.BridgeScene,
                     CadSessionActionIds.SetStudioWorkspace,
                     CadSessionActionIds.ExtrudeProfile,
                     CadSessionActionIds.AddRect,
                 })
            await Assert.That(cadIds.Contains(id)).IsTrue().Because(id);

        var sceneIds = scene.Actions().Actions.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in new[]
                 {
                     SceneSessionActionIds.SetMeshMaterial,
                     SceneSessionActionIds.EnsureStudioLights,
                     SceneSessionActionIds.SaveRenderPng,
                     SceneSessionActionIds.DescribeScene,
                 })
            await Assert.That(sceneIds.Contains(id)).IsTrue().Because(id);
    }
}
