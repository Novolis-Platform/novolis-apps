using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Cad.Ship.Core;
using Novolis.Avalonia.Ship;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Services;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Cad.Primitives;
using Novolis.Ship.Analysis;
using Novolis.Ship.Design;
using Novolis.Ship.Primitives;

namespace ShipDesigner;

/// <summary>Headless checks: object-first create/save, cutouts, analysis, Calypso import.</summary>
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
            var design = new ShipDesignSession(root);
            ShipDesignChrome.Attach(cad, design);
            Check("clean slate start", !design.HasShip && doc.Document.Entities.Count == 0);

            design.NewShip(new ShipDefinition
            {
                Name = "Smoke Freighter",
                Length = ShipLengths.FromMeters(90f),
                Beam = ShipLengths.FromMeters(20f),
                Height = ShipLengths.FromMeters(12f),
                DeckCount = 3,
                DeckSpacing = ShipLengths.FromMeters(4f),
                HullMaterial = MaterialId.Steel,
                PrimaryStructuralMaterial = MaterialId.Steel,
                HullThickness = ShipLengths.FromMeters(0.024f),
                FrameSpacing = ShipLengths.FromMeters(1.5f),
                HullGenerator = HullGeneratorKind.Faceted,
                GravitySystem = GravitySystemKind.Plating,
                NominalGravityG = 1f,
                NominalInternalPressureAtm = 1f,
                ExternalEnvironment = ExternalEnvironmentKind.Vacuum,
            });
            Check("factory hull entities", design.Design.Hull.Geometry.Entities.Count > 0);
            Check("factory frames", design.Design.Frames.Count > 0);
            Check("environment seeded", design.Design.Environment.External == ExternalEnvironmentKind.Vacuum);
            Check("load cases seeded", design.Design.LoadCases.Count >= 4);
            Check("cad mirror populated", doc.Document.Entities.Count > 0, $"count={doc.Document.Entities.Count}");
            Check("analysis mass", design.Analysis.TotalMassKg > 1000f);
            Check("analysis categories", design.Analysis.Categories.Count == 6);

            Check("has ship after create", design.HasShip);
            var deck = design.Design.Decks[1];
            design.Mutate(d => ShipDesignMutations.AddPassage(
                d, deck.Id, "Main Corridor", [[0f, -20f], [0f, 20f]], 1.2f, 2.2f));
            Check("passage cutouts", design.Design.Cutouts.Count > 0);
            design.Mutate(d => ShipDesignMutations.AddCompartment(
                d, deck.Id, "Cargo Hold", [[-6f, -8f], [6f, -8f], [6f, 8f], [-6f, 8f]]));
            design.Mutate(d => ShipDesignMutations.AddBulkhead(
                d, deck.Id, "BH-Extra", [[-8f, 4f], [8f, 4f]], 0.08f, 3.2f));
            design.Mutate(d => ShipDesignMutations.AddEquipment(
                d, "Pump", [2f, 4f, 1f], [0.6f, 0.6f, 0.8f], 250f));
            Check("compartment created", design.Design.Compartments.Count >= 1);
            Check("bulkhead created", design.Design.Bulkheads.Count >= 4);
            Check("equipment created", design.Design.Equipment.Count >= 1);

            design.ClearToBlank();
            Check("clear to blank", !design.HasShip && doc.Document.Entities.Count == 0);
            design.NewShip(ShipDesignSession.DefaultDefinition("Smoke Freighter"));
            deck = design.Design.Decks[1];

            for (var i = 0; i < 8; i++)
            {
                var n = i;
                design.Mutate(d => ShipDesignMutations.AddPassage(
                    d, deck.Id, $"P{n}", [[n - 4f, -25f], [n - 4f, 25f]], 1.2f, 2.2f));
            }

            Check("structure analysis reacts", design.Analysis.StatusOf(AnalysisCategory.Structure) != AnalysisSeverity.Green);

            var shipPath = Path.Combine(root, "smoke.shipjson");
            design.SaveTo(shipPath);
            Check("save shipjson", File.Exists(shipPath));
            design.OpenFromPath(shipPath);
            Check("reopen shipjson", design.Design.Frames.Count > 0);
            Check("schema v2", design.Design.SchemaVersion >= 2);
            Check("passages survive reopen", design.Design.Passages.Count >= 1);

            var scenePath = Path.Combine(root, "analyze.nov3djson");
            var eval = ShipDesignEvaluator.Evaluate(design.Design, scenePath);
            Check("evaluate objects", eval.ObjectCount > 0);
            Check("evaluate scene file", File.Exists(scenePath));

            var val = ShipDesignValidator.Validate(design.Design);
            Check("design validation runs", val.Issues.All(i => i.Code != "SHIP_HULL_EMPTY"));

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
            doc.Document.Entities.Clear();
            doc.Document.Entities.AddRange([wall, space, door]);
            var validate = cad.Execute(new CadCommandDto { ActionId = ShipChrome.ValidateShipActionId });
            Check("legacy validateship", validate.Ok, validate.Message);

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
                if (imported.Ok)
                {
                    design.ImportCadDocument(doc.Document);
                    Check("calypso → ShipDesign", design.Design.Hull.Geometry.Entities.Count > 0
                        || design.Design.Compartments.Count > 0);
                }
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
