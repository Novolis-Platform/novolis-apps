using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Ship.Design;

namespace CadStudio.Unit;

public sealed class ShipModeTests
{
    [Test]
    public async Task ShipChrome_AttachesOnlyToShipCadSession()
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
            cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.AddRect,
                Properties = new Dictionary<string, string>
                {
                    ["a"] = "0,0,0",
                    ["b"] = "2,0,2",
                },
            });
            var productCount = document.Document.Entities.Count;
            var shipCadHost = new CadStudio.ShipCadSession(root);
            var shipCad = shipCadHost.Service;
            var ship = new ShipDesignSession(shipCadHost.Settings.DataRoot);

            await Assert.That(cad.ExteriorHooks).IsNull();
            await Assert.That(cad.Actions().Actions.Any(
                action => action.Id == CadShipChrome.ImportShipActionId)).IsFalse();

            using (ShipDesignChrome.Attach(shipCad, ship))
            {
                ship.NewShip(ShipDesignSession.DefaultDefinition("Unit Ship"));
                ship.SetWorkspace(ShipWorkspaceKind.Model);
                ship.Select(ship.Design.Hull.Id.AsObject());
                ship.Notify();

                await Assert.That(ship.HasShip).IsTrue();
                await Assert.That(ship.Validation).IsNotNull();
                await Assert.That(ship.Analysis.TotalMassKg).IsGreaterThan(0);
                await Assert.That(shipCad.Document.Document.Entities.Count).IsGreaterThan(0);
                await Assert.That(document.Document.Entities.Count).IsEqualTo(productCount);
                await Assert.That(cad.ExteriorHooks).IsNull();
                await Assert.That(shipCad.ExteriorHooks).IsNotNull();
                await Assert.That(cad.Actions().Actions.Any(
                    action => action.Id == CadShipChrome.ImportShipActionId)).IsFalse();
                await Assert.That(shipCad.Actions().Actions.Any(
                    action => action.Id == CadShipChrome.ImportShipActionId)).IsTrue();

                var path = Path.Combine(root, "unit.shipjson");
                ship.SaveTo(path);
                ship.OpenFromPath(path);
                await Assert.That(ship.IsDirty).IsFalse();
            }

            await Assert.That(shipCad.ExteriorHooks).IsNull();
            await Assert.That(shipCad.Actions().Actions.Any(
                action => action.Id == CadShipChrome.ImportShipActionId)).IsFalse();
            await Assert.That(document.Document.Entities.Count).IsEqualTo(productCount);
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
