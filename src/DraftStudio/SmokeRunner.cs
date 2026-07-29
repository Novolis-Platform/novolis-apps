using System.Text.Json;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Commands.Expressions;
using Novolis.Math.Geometry;

namespace DraftStudio;

/// <summary>Headless pipeline check: DSL → document → .cadjson → .cadphys.json.</summary>
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

        Console.WriteLine("Draft Studio smoke");

        var parse = FunctionCallParser.TryParse("Line(0, 0, 2, 0)");
        Check("FunctionCallParser Line", parse.Success && parse.Call!.Name == "Line" && parse.Call.Arguments.Count == 4);

        var (degree, controls, knots, weights) = NurbsCurve.FromFitPoints(
        [
            new(0, 0, 0),
            new(1, 0, 1),
            new(2, 0, 0),
            new(3, 0, 1),
        ]);
        var samples = NurbsCurve.Tessellate(degree, controls, knots, weights, 16);
        Check("NurbsCurve tessellate", samples.Length == 16);

        var root = Path.Combine(Path.GetTempPath(), "novolis-draft-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new CadEditorSettings(root);
            var session = new CadDocumentSession(settings);
            var bus = new CadCommandBus(session);
            var dispatcher = new CadCommandDispatcher(session, bus, settings);

            session.OpenOrCreateDefault();
            Check("starter entities", session.Document.Entities.Count >= 2);

            Check("dispatch Line", dispatcher.TryDispatch("Line(1,1,3,3)") is null);
            Check("dispatch Circle", dispatcher.TryDispatch("Circle(0,0,1.5)") is null);
            Check("dispatch Spline", dispatcher.TryDispatch("Spline(0,0,1,1,2,0,3,1)") is null);
            Check("dispatch Box", dispatcher.TryDispatch("Box(2,1,4)") is null);

            var spline = session.Document.Entities.FirstOrDefault(e => e.Kind == "spline");
            Check(
                "spline has NURBS",
                spline is not null
                && spline.ControlPoints is { Count: >= 2 }
                && spline.Knots is { Length: > 0 }
                && spline.Degree >= 1);

            var box = session.Document.Entities.FirstOrDefault(e => e.Kind == "box");
            Check("box analytic halfExtents", box?.HalfExtents is { Length: >= 3 });

            session.SelectedId = box?.Id;
            Check("dispatch Move", dispatcher.TryDispatch("Move(0.5,0,0)") is null);
            bus.Undo();
            Check("undo", bus.CanRedo);

            session.Save();
            Check("cadjson written", File.Exists(settings.DocumentPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(settings.DocumentPath));
            Check("cadjson format", doc.RootElement.GetProperty("format").GetString() == "novolis.cad");
            Check("cadjson schemaVersion", doc.RootElement.GetProperty("schemaVersion").GetInt32() == 1);

            var exporter = new CadPhysExporter();
            var phys = exporter.Build(session.Document);
            exporter.Write(phys, settings.PhysDocumentPath);
            Check("phys written", File.Exists(settings.PhysDocumentPath));
            Check("phys has mesh", phys.Meshes.Count >= 1);
            Check("phys has collider", phys.Colliders.Count >= 1);
            Check("phys mesh indices % 3", phys.Meshes.All(m => m.Indices.Count % 3 == 0));

            var reloaded = new CadDocumentSession(settings);
            reloaded.OpenOrCreateDefault();
            Check("reload entity count", reloaded.Document.Entities.Count == session.Document.Entities.Count);
            Check("reload keeps spline", reloaded.Document.Entities.Any(e => e.Kind == "spline"));

            var shipSrc = CadShipImport.ResolveSourceCadjson();
            if (shipSrc is not null)
            {
                var imported = CadShipImport.ImportIntoWorkspace(root, shipSrc);
                var ship = new CadDocumentSession(new CadEditorSettings(root, CadShipImport.WorkspaceFolderName));
                ship.OpenFromPath(imported);
                Check("ship import entities", ship.Document.Entities.Count >= 100);
                Check("ship has walls", ship.Document.Entities.Any(e => e.Kind == "wall"));
                Check("ship has spaces", ship.Document.Entities.Any(e => e.Kind == "space"));
                var (center, radius) = EntityBounds.Compute(ship.Document);
                Check("ship bounds", radius > 10f, $"radius={radius} center={center}");
            }
            else
            {
                Console.WriteLine("  SKIP ship import (no generated *.cadjson under LocalAppData/Novolis)");
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best effort
            }
        }

        Console.WriteLine(failures == 0 ? "SMOKE_OK" : $"SMOKE_FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }
}
