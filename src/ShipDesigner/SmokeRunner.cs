using System.Text.Json;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Cad.Ship.Core;
using Novolis.Avalonia.Ship;
using Novolis.Avalonia.Ship.Services;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Topology;
using Novolis.Ship.Validation;

namespace ShipDesigner;

/// <summary>Headless checks: chrome, Calypso import, hatch save round-trip, airtight, exterior.</summary>
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

            ShipDocumentMetrics.SetShipEnvelope(doc.Document, 69, 20, 12, 4);
            var wall = new CadEntity
            {
                Kind = "wall",
                Name = "W",
                Deck = 0,
                A = [0, 0, 0],
                B = [0, 0, 4],
                Height = 2.4f,
                Thickness = 0.15f,
            };
            var space = new CadEntity
            {
                Kind = "space",
                Name = "Hold",
                Deck = 0,
                Height = 2.4f,
                Points = [[-2, 0, 0], [2, 0, 0], [2, 0, 4], [-2, 0, 4]],
            };
            var door = new CadEntity
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
            Check("airtight paints space color", space.Color is { Length: >= 3 });

            var place = cad.Execute(new CadCommandDto
            {
                ActionId = ShipChrome.PlaceHatchActionId,
                Properties = new Dictionary<string, string>
                {
                    ["hostWallId"] = wall.Id.ToString(),
                    ["clearWidth"] = "1.2",
                    ["clearHeight"] = "2.1",
                    ["name"] = "SmokeHatch",
                },
            });
            Check("placehatch action", place.Ok, place.Message);

            var path = Path.Combine(root, "smoke.cadjson");
            doc.SaveTo(path);
            Check("save cadjson", File.Exists(path));

            doc.NewDocument();
            doc.OpenFromPath(path);
            Check("reopen cadjson", doc.Document.Entities.Count >= 4);
            Check(
                "hatch survives reopen",
                ShipCad.Openings(doc.Document).Any(o =>
                    string.Equals(o.Name, "SmokeHatch", StringComparison.OrdinalIgnoreCase)));

            var narrow = ShipCad.Openings(doc.Document).First(o =>
                string.Equals(o.Name, "SmokeHatch", StringComparison.OrdinalIgnoreCase));
            ShipCad.TagOpeningPressure(narrow, ShipPressureClass.Habitable, 0.8f, 2.2f);
            var fail = ShipValidator.Validate(doc.Document);
            Check("narrow door fails validation", !fail.Ok);

            var exterior = new CadEntity
            {
                Kind = "box",
                Name = "ext-hull",
                Center = [0, 6, 0],
                HalfExtents = [32.5f, 6f, 10f],
                Properties = new Dictionary<string, JsonElement>
                {
                    [ShipPropertyKeys.Exterior] = JsonSerializer.SerializeToElement(true),
                },
            };
            doc.Document.Entities.Add(exterior);
            var exteriorPath = Path.Combine(root, "with-exterior.cadjson");
            doc.SaveTo(exteriorPath);
            doc.NewDocument();
            doc.OpenFromPath(exteriorPath);
            Check(
                "exterior solid round-trip",
                doc.Document.Entities.Any(ShipCad.IsExteriorSolid));

            var calypso = CadShipImport.ResolveSourceCadjson();
            if (calypso is null)
            {
                Console.WriteLine("  SKIP Calypso seed (no %LocalAppData%\\Novolis\\*\\generated\\*.cadjson)");
            }
            else
            {
                Console.WriteLine($"  … importing Calypso seed {calypso}");
                var imported = cad.Execute(new CadCommandDto { ActionId = CadShipChrome.ImportShipActionId });
                Check("importship Calypso seed", imported.Ok, imported.Message);
                Check("imported has walls/spaces", doc.Document.Entities.Count > 10, $"count={doc.Document.Entities.Count}");
                var topo = ShipTopology.Analyze(doc.Document);
                ShipTopology.ApplySpaceFlags(doc.Document, topo);
                ShipAirtightOverlay.Apply(doc.Document, topo);
                Check("Calypso airtight analyze", topo.SpaceIds.Count > 0);
                var pascalClear = ShipCad.Openings(doc.Document)
                    .Select(o => ShipCad.GetClearWidth(o, fallback: -1f))
                    .Any(w => w >= 1.0f);
                Check("Calypso clearWidth readable", pascalClear);
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }

        Console.WriteLine(failures == 0 ? "Ship Designer smoke OK" : $"Ship Designer smoke FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }
}
