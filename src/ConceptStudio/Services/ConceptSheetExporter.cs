using System.Globalization;
using System.Numerics;
using System.Text;
using ConceptStudio.Models;

namespace ConceptStudio.Services;

internal static class ConceptSheetExporter
{
    public static string ExportSvg(ConceptDocument document, string title)
    {
        var (center, radius) = SceneBounds.Compute(document);
        var scale = document.UnitScaleMeters;
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="800" viewBox="0 0 1200 800">""");
        sb.AppendLine("""<rect width="1200" height="800" fill="#f4f4f0"/>""");
        sb.AppendLine($"""<text x="40" y="36" font-family="Segoe UI, sans-serif" font-size="20" fill="#222">{Escape(title)}</text>""");
        sb.AppendLine($"""<text x="40" y="58" font-family="Segoe UI, sans-serif" font-size="12" fill="#555">Scale 1 unit = {scale.ToString(CultureInfo.InvariantCulture)} m</text>""");

        DrawView(sb, document, ConceptViewMode.Plan, "Plan", 40, 80, 340, 300, center, radius);
        DrawView(sb, document, ConceptViewMode.Profile, "Profile", 420, 80, 340, 300, center, radius);
        DrawView(sb, document, ConceptViewMode.Bow, "Bow", 800, 80, 340, 300, center, radius);

        foreach (var ann in document.Annotations)
            DrawAnnotation(sb, ann, center, radius, scale);

        sb.AppendLine("""<rect x="40" y="720" width="1120" height="60" fill="none" stroke="#888" stroke-width="1"/>""");
        sb.AppendLine($"""<text x="52" y="752" font-family="Segoe UI, sans-serif" font-size="11" fill="#444">Concept Studio — {DateTime.Now:yyyy-MM-dd}</text>""");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void DrawView(StringBuilder sb, ConceptDocument document, ConceptViewMode mode, string label, float x, float y, float w, float h, Vector3 center, float radius)
    {
        sb.AppendLine($"""<rect x="{x.ToString(CultureInfo.InvariantCulture)}" y="{y.ToString(CultureInfo.InvariantCulture)}" width="{w.ToString(CultureInfo.InvariantCulture)}" height="{h.ToString(CultureInfo.InvariantCulture)}" fill="#fff" stroke="#999"/>""");
        sb.AppendLine($"""<text x="{(x + 8).ToString(CultureInfo.InvariantCulture)}" y="{(y + 18).ToString(CultureInfo.InvariantCulture)}" font-family="Segoe UI, sans-serif" font-size="13" fill="#333">{label}</text>""");

        var parts = document.Parts.Where(p => !p.IsGroup).ToList();
        foreach (var part in parts)
        {
            var color = ColorHex(part.Color);
            var rect = ProjectBounds(part, mode, center, radius, x, y, w, h);
            if (rect is null)
                continue;

            sb.AppendLine($"""<rect x="{rect.Value.X.ToString(CultureInfo.InvariantCulture)}" y="{rect.Value.Y.ToString(CultureInfo.InvariantCulture)}" width="{rect.Value.W.ToString(CultureInfo.InvariantCulture)}" height="{rect.Value.H.ToString(CultureInfo.InvariantCulture)}" fill="{color}" fill-opacity="0.55" stroke="#222" stroke-width="1"/>""");
        }
    }

    private static void DrawAnnotation(StringBuilder sb, AnnotationRecord ann, Vector3 center, float radius, float unitScale)
    {
        var viewBox = ann.View.ToLowerInvariant() switch
        {
            "plan" => (40f, 80f, 340f, 300f, ConceptViewMode.Plan),
            "bow" => (800f, 80f, 340f, 300f, ConceptViewMode.Bow),
            _ => (420f, 80f, 340f, 300f, ConceptViewMode.Profile),
        };

        var from = new Vector3(ann.From.Length > 0 ? ann.From[0] : 0, ann.From.Length > 1 ? ann.From[1] : 0, ann.From.Length > 2 ? ann.From[2] : 0);
        var to = new Vector3(ann.To.Length > 0 ? ann.To[0] : 1, ann.To.Length > 1 ? ann.To[1] : 0, ann.To.Length > 2 ? ann.To[2] : 0);
        var p0 = ProjectPoint(from, viewBox.Item5, center, radius, viewBox.Item1, viewBox.Item2, viewBox.Item3, viewBox.Item4);
        var p1 = ProjectPoint(to, viewBox.Item5, center, radius, viewBox.Item1, viewBox.Item2, viewBox.Item3, viewBox.Item4);
        var dist = Vector3.Distance(from, to) * unitScale;
        var label = string.IsNullOrWhiteSpace(ann.Label) ? $"{dist:0.#} m" : ann.Label!;

        sb.AppendLine($"""<line x1="{p0.X.ToString(CultureInfo.InvariantCulture)}" y1="{p0.Y.ToString(CultureInfo.InvariantCulture)}" x2="{p1.X.ToString(CultureInfo.InvariantCulture)}" y2="{p1.Y.ToString(CultureInfo.InvariantCulture)}" stroke="#c00" stroke-width="1.5"/>""");
        sb.AppendLine($"""<text x="{((p0.X + p1.X) * 0.5f).ToString(CultureInfo.InvariantCulture)}" y="{((p0.Y + p1.Y) * 0.5f - 6f).ToString(CultureInfo.InvariantCulture)}" font-family="Segoe UI, sans-serif" font-size="11" fill="#900">{Escape(label)}</text>""");
    }

    private static (float X, float Y, float W, float H)? ProjectBounds(ConceptPartRecord part, ConceptViewMode mode, Vector3 center, float radius, float vx, float vy, float vw, float vh)
    {
        var c = ConceptMaterialMapper.ToVector3(part.Center);
        var he = part.HalfExtents.Length >= 3
            ? new Vector3(part.HalfExtents[0], part.HalfExtents[1], part.HalfExtents[2])
            : new Vector3(part.Radius);

        var corners = new[]
        {
            c + new Vector3(-he.X, -he.Y, -he.Z),
            c + new Vector3(he.X, he.Y, he.Z),
        };

        var p0 = ProjectPoint(corners[0], mode, center, radius, vx, vy, vw, vh);
        var p1 = ProjectPoint(corners[1], mode, center, radius, vx, vy, vw, vh);
        var minX = MathF.Min(p0.X, p1.X);
        var minY = MathF.Min(p0.Y, p1.Y);
        var maxX = MathF.Max(p0.X, p1.X);
        var maxY = MathF.Max(p0.Y, p1.Y);
        return (minX, minY, MathF.Max(4f, maxX - minX), MathF.Max(4f, maxY - minY));
    }

    private static (float X, float Y) ProjectPoint(Vector3 world, ConceptViewMode mode, Vector3 center, float radius, float vx, float vy, float vw, float vh)
    {
        var rel = world - center;
        var u = mode switch
        {
            ConceptViewMode.Plan => new Vector2(rel.X, rel.Z),
            ConceptViewMode.Profile => new Vector2(rel.Z, rel.Y),
            _ => new Vector2(rel.X, rel.Y),
        };

        var extent = radius * 2.2f;
        var nx = u.X / extent + 0.5f;
        var ny = 0.5f - u.Y / extent;
        return (vx + nx * vw, vy + ny * vh);
    }

    private static string ColorHex(float[] rgb)
    {
        var r = rgb.Length > 0 ? (int)(rgb[0] * 255) : 180;
        var g = rgb.Length > 1 ? (int)(rgb[1] * 255) : 180;
        var b = rgb.Length > 2 ? (int)(rgb[2] * 255) : 180;
        return $"#{r:x2}{g:x2}{b:x2}";
    }

    private static string Escape(string text) =>
        System.Security.SecurityElement.Escape(text) ?? string.Empty;
}
