using Avalonia;
using Novolis.Avalonia.Controls.Sketch;

namespace SketchStudio;

/// <summary>Headless pipeline check: document ops → .sketchjson → SVG/PNG export.</summary>
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

        Console.WriteLine("Sketch Studio smoke");

        var doc = new SketchDocument { Version = 2 };
        doc.Grid.Size = 10;
        doc.AddStroke(new StrokeShape
        {
            Id = "a",
            StrokeColor = "#112233",
            StrokeWidth = 2,
            Points = [new SketchPoint(3, 3), new SketchPoint(17, 3), new SketchPoint(17, 18)]
        });
        doc.AddStroke(new StrokeShape
        {
            Id = "b",
            Points = [new SketchPoint(20, 0), new SketchPoint(30, 0)]
        });
        doc.AddStroke(new StrokeShape
        {
            Id = "t1",
            Kind = SketchElementKind.Text,
            Text = "Smoke",
            FontSize = 18,
            RotationDegrees = 5,
            StrokeColor = "#445566",
            Points = [new SketchPoint(5, 40), new SketchPoint(40, 60)]
        });

        doc.Select("a");
        doc.GridifySelection();
        Check(
            "gridify quantized",
            doc.Find("a")!.Points is [{ X: 0, Y: 0 }, { X: 20, Y: 0 }, { X: 20, Y: 20 }]);

        Check("undo after gridify", doc.Undo());
        Check(
            "undo restored geometry",
            doc.Find("a")!.Points is [{ X: 3, Y: 3 }, { X: 17, Y: 3 }, { X: 17, Y: 18 }]);

        doc.SetSelection(["a", "b"]);
        Check("fuse", doc.FuseSelection());
        var groupId = doc.Find("a")!.GroupId;
        Check("fuse shares groupId", groupId is not null && groupId == doc.Find("b")!.GroupId);

        var overlay = doc.AddLayer("Overlay");
        Check("layer added", doc.Layers.Count >= 2);
        doc.ActiveLayerId = overlay.Id;
        doc.AddStroke(new StrokeShape
        {
            Id = "on-overlay",
            Closed = true,
            Points =
            [
                new SketchPoint(0, 0),
                new SketchPoint(5, 0),
                new SketchPoint(5, 5),
                new SketchPoint(0, 0)
            ]
        });
        Check("stroke on active layer", doc.Find("on-overlay")!.LayerId == overlay.Id);
        Check("apply fill", doc.ApplyFill("on-overlay", "#80ff0000"));
        Check("fill color", doc.Find("on-overlay")!.FillColor == "#80ff0000");

        // Flood-fill pocket between four edges.
        doc.AddStroke(new StrokeShape
        {
            Id = "ft",
            StrokeWidth = 2,
            Points = [new SketchPoint(20, 20), new SketchPoint(40, 20)]
        });
        doc.AddStroke(new StrokeShape
        {
            Id = "fr",
            StrokeWidth = 2,
            Points = [new SketchPoint(40, 20), new SketchPoint(40, 40)]
        });
        doc.AddStroke(new StrokeShape
        {
            Id = "fb",
            StrokeWidth = 2,
            Points = [new SketchPoint(40, 40), new SketchPoint(20, 40)]
        });
        doc.AddStroke(new StrokeShape
        {
            Id = "fl",
            StrokeWidth = 2,
            Points = [new SketchPoint(20, 40), new SketchPoint(20, 20)]
        });
        var beforeFlood = doc.Elements.Count;
        Check("flood fill", doc.TryFloodFill(new SketchPoint(30, 30), "#ffe63946"));
        Check("flood added element", doc.Elements.Count == beforeFlood + 1);
        Check("flood fill color", doc.Elements[^1].FillColor == "#ffe63946");

        var json = SketchJson.Serialize(doc);
        var loaded = SketchJson.Deserialize(json);
        Check("json version", loaded.Version >= 3);
        Check("json layers", loaded.Layers.Count >= 2);
        Check("json element count", loaded.Elements.Count == doc.Elements.Count);
        var text = loaded.Elements.FirstOrDefault(e => e.Id == "t1");
        Check("json text kind", text?.Kind == SketchElementKind.Text && text.Text == "Smoke");
        Check("json text font", text is { FontSize: 18 });
        Check("json rotation", text is { RotationDegrees: 5 });
        Check("json group", loaded.Find("a")?.GroupId == groupId);
        Check("json fill", loaded.Find("on-overlay")?.FillColor == "#80ff0000");

        var svg = SketchExport.ToSvg(doc);
        Check("svg export", svg.Contains("<svg", StringComparison.Ordinal) && svg.Length > 80);

        try
        {
            Program.BuildAvaloniaApp().SetupWithoutStarting();
            var png = SketchExport.ToPng(doc, opaqueBackground: true);
            Check("png export", png.Length > 32);
        }
        catch (Exception ex)
        {
            Check("png export", false, ex.Message);
        }

        Console.WriteLine(failures == 0 ? "Sketch Studio smoke: PASS" : $"Sketch Studio smoke: FAIL ({failures})");
        return failures == 0 ? 0 : 1;
    }
}
