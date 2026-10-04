using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Ship.Design;

namespace CadStudio3D.Unit;

public sealed class ShipModeTests
{
    [Test]
    public async Task ShipChrome_IsAttachedOnlyForShipMode()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "novolis-cadstudio-ship-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new CadEditorSettings(root);
            var document = new CadDocumentSession(settings);
            var bus = new CadCommandBus(document);
            var dispatcher = new CadCommandDispatcher(document, bus, settings);
            var cad = new CadSessionService(document, settings, bus, dispatcher);
            var ship = new ShipDesignSession(Path.Combine(root, "ships"));

            await Assert.That(cad.ExteriorHooks).IsNull();
            await Assert.That(cad.Actions().Actions.Any(
                action => action.Id == CadShipChrome.ImportShipActionId)).IsFalse();

            using (ShipDesignChrome.Attach(cad, ship))
            {
                ship.NewShip(ShipDesignSession.DefaultDefinition("Unit Ship"));
                ship.SetWorkspace(ShipWorkspaceKind.Model);
                ship.Select(ship.Design.Hull.Id.AsObject());
                ship.Notify();

                await Assert.That(ship.HasShip).IsTrue();
                await Assert.That(ship.Validation).IsNotNull();
                await Assert.That(ship.Analysis.TotalMassKg).IsGreaterThan(0);
                await Assert.That(document.Document.Entities.Count).IsGreaterThan(0);
                await Assert.That(cad.ExteriorHooks).IsNotNull();
                await Assert.That(cad.Actions().Actions.Any(
                    action => action.Id == CadShipChrome.ImportShipActionId)).IsTrue();

                var path = Path.Combine(root, "unit.shipjson");
                ship.SaveTo(path);
                ship.OpenFromPath(path);
                await Assert.That(ship.IsDirty).IsFalse();
            }

            await Assert.That(cad.ExteriorHooks).IsNull();
            await Assert.That(cad.Actions().Actions.Any(
                action => action.Id == CadShipChrome.ImportShipActionId)).IsFalse();
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
