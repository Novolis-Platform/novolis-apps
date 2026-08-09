using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Ship;
using Novolis.Ship.Primitives;
using Novolis.Ship.Topology;
using Novolis.Ship.Validation;

namespace ShipDesigner;

/// <summary>Headless checks: attach chrome, validate fixture, import path resolve.</summary>
internal static class SmokeRunner
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        Console.WriteLine("Ship Designer smoke");

        var root = Path.Combine(Path.GetTempPath(), "novolis-ship-designer-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new CadEditorSettings(root);
            var doc = new CadDocumentSession(settings);
            doc.NewDocument();
            var bus = new CadCommandBus(doc);
            var dispatcher = new CadCommandDispatcher(doc, bus, settings);
            var cad = new CadSessionService(doc, settings, bus, dispatcher)
            {
                AppId = "ship-designer-smoke",
                AppTitle = "Ship Designer Smoke",
            };
            ShipChrome.Attach(cad);

            ShipDocumentMetrics.SetShipEnvelope(doc.Document, 65, 20, 12, 4);
            var wall = new Novolis.Cad.Primitives.CadEntity
            {
                Kind = "wall",
                Name = "W",
                Deck = 0,
                A = [0, 0, 0],
                B = [0, 0, 4],
                Height = 2.4f,
                Thickness = 0.15f,
            };
            var space = new Novolis.Cad.Primitives.CadEntity
            {
                Kind = "space",
                Name = "Hold",
                Deck = 0,
                Height = 2.4f,
                Points = [[-2, 0, 0], [2, 0, 0], [2, 0, 4], [-2, 0, 4]],
            };
            var door = new Novolis.Cad.Primitives.CadEntity
            {
                Kind = "opening",
                Name = "Door",
                OpeningType = "door",
                Deck = 0,
                HostWallId = wall.Id,
                Height = 2.2f,
                Footprint = [[-0.5f, 0, 1.5f], [0.5f, 0, 1.5f], [0.5f, 0, 2.5f], [-0.5f, 0, 2.5f]],
            };
            ShipCad.TagOpeningPressure(door, ShipPressureClass.Habitable, 1.1f, 2.2f);
            doc.Document.Entities.AddRange([wall, space, door]);

            var validate = cad.Execute(new CadCommandDto { ActionId = ShipChrome.ValidateShipActionId });
            Check("validateship action", validate.Ok, validate.Message);

            var airtight = cad.Execute(new CadCommandDto { ActionId = ShipChrome.RefreshAirtightActionId });
            Check("refreshairtight action", airtight.Ok, airtight.Message);

            var topo = ShipTopology.Analyze(doc.Document);
            Check("topology ran", topo.SpaceIds.Count == 1);

            var path = Path.Combine(root, "smoke.cadjson");
            doc.SaveTo(path);
            Check("save cadjson", File.Exists(path));

            doc.NewDocument();
            doc.OpenFromPath(path);
            Check("reopen cadjson", doc.Document.Entities.Count >= 3);

            var narrow = ShipCad.Openings(doc.Document).First();
            ShipCad.TagOpeningPressure(narrow, ShipPressureClass.Habitable, 0.8f, 2.2f);
            var fail = ShipValidator.Validate(doc.Document);
            Check("narrow door fails validation", !fail.Ok);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }

        Console.WriteLine(failures == 0 ? "Ship Designer smoke OK" : $"Ship Designer smoke FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }
}
