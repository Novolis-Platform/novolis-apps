using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Cad.Primitives;

namespace CadStudio.Unit;

public sealed class RaylibHostIsolationTests
{
    [Test]
    public async Task Dual_editor_surfaces_leave_preview_hosts_inactive()
    {
        var (cadSettings, cadSession, cadBus, cadDispatcher) = DraftTestHarness.Create();
        var (shipSettings, shipSession, shipBus, shipDispatcher) = DraftTestHarness.Create();

        var cad = new CadEditorSurface(
            cadSession,
            cadSettings,
            cadBus,
            cadDispatcher,
            new CadToolController(cadDispatcher, cadSettings),
            new CadModelRenderer(cadSession, cadSettings));
        var ship = new CadEditorSurface(
            shipSession,
            shipSettings,
            shipBus,
            shipDispatcher,
            new CadToolController(shipDispatcher, shipSettings),
            new CadModelRenderer(shipSession, shipSettings));

        await Assert.That(cad.Workspace).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(ship.Workspace).IsEqualTo(CadWorkspace.Cad);
        await Assert.That(cad.ModelHost.IsVisible).IsFalse();
        await Assert.That(ship.ModelHost.IsVisible).IsFalse();
        await Assert.That(cad.ModelHost.IsHostRunning).IsFalse();
        await Assert.That(ship.ModelHost.IsHostRunning).IsFalse();

        CadStudioRaylibHosts.ReleaseHidden(cad, ship, cadVisible: true, shipVisible: false);
        await Assert.That(cad.ModelHost.IsHostRunning).IsFalse();
        await Assert.That(ship.ModelHost.IsHostRunning).IsFalse();

        CadStudioRaylibHosts.ReleaseHidden(cad, ship, cadVisible: false, shipVisible: true);
        await Assert.That(cad.ModelHost.IsHostRunning).IsFalse();
        await Assert.That(ship.ModelHost.IsHostRunning).IsFalse();
    }

    [Test]
    public async Task Preview_workspace_does_not_leave_the_sibling_host_running()
    {
        var (cadSettings, cadSession, cadBus, cadDispatcher) = DraftTestHarness.Create();
        var (shipSettings, shipSession, shipBus, shipDispatcher) = DraftTestHarness.Create();

        var cad = new CadEditorSurface(
            cadSession,
            cadSettings,
            cadBus,
            cadDispatcher,
            new CadToolController(cadDispatcher, cadSettings),
            new CadModelRenderer(cadSession, cadSettings));
        var ship = new CadEditorSurface(
            shipSession,
            shipSettings,
            shipBus,
            shipDispatcher,
            new CadToolController(shipDispatcher, shipSettings),
            new CadModelRenderer(shipSession, shipSettings));

        cad.SetWorkspace(CadWorkspace.Preview);
        await Assert.That(cad.ModelHost.IsVisible).IsTrue();
        await Assert.That(ship.ModelHost.IsVisible).IsFalse();

        CadStudioRaylibHosts.ReleaseHidden(cad, ship, cadVisible: false, shipVisible: false);
        await Assert.That(cad.ModelHost.IsHostRunning).IsFalse();
        await Assert.That(ship.ModelHost.IsHostRunning).IsFalse();
    }
}
