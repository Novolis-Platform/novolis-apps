using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ConceptStudio.Models;

namespace ConceptStudio.Ui;

internal sealed class DimensionOverlay : Canvas
{
    private ConceptDocument? _document;
    private ConceptViewMode _viewMode;

    public void Update(ConceptDocument? document, ConceptViewMode viewMode)
    {
        _document = document;
        _viewMode = viewMode;
        Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        if (_document is null || Bounds.Width < 1 || Bounds.Height < 1)
            return;

        var viewName = _viewMode switch
        {
            ConceptViewMode.Plan => "plan",
            ConceptViewMode.Profile => "profile",
            ConceptViewMode.Bow => "bow",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(viewName))
            return;

        var (center, radius) = Services.SceneBounds.Compute(_document);

        foreach (var ann in _document.Annotations.Where(a => a.View.Equals(viewName, StringComparison.OrdinalIgnoreCase)))
        {
            var from = ToVector3(ann.From);
            var to = ToVector3(ann.To);
            var p0 = Project(from, center, radius);
            var p1 = Project(to, center, radius);

            Children.Add(new Line
            {
                StartPoint = p0,
                EndPoint = p1,
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2,
            });

            var dist = Vector3.Distance(from, to) * _document.UnitScaleMeters;
            var label = string.IsNullOrWhiteSpace(ann.Label) ? $"{dist:0.#} m" : ann.Label!;
            var text = new TextBlock
            {
                Text = label,
                Foreground = Brushes.OrangeRed,
                FontSize = 12,
            };
            Canvas.SetLeft(text, (p0.X + p1.X) * 0.5);
            Canvas.SetTop(text, (p0.Y + p1.Y) * 0.5 - 14);
            Children.Add(text);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        Rebuild();
        return size;
    }

    private Point Project(Vector3 world, Vector3 center, float radius)
    {
        var rel = world - center;
        var u = _viewMode switch
        {
            ConceptViewMode.Plan => new Vector2(rel.X, rel.Z),
            ConceptViewMode.Profile => new Vector2(rel.Z, rel.Y),
            ConceptViewMode.Bow => new Vector2(rel.X, rel.Y),
            _ => Vector2.Zero,
        };

        var extent = radius * 2.2f;
        var nx = u.X / extent + 0.5f;
        var ny = 0.5f - u.Y / extent;
        return new Point(nx * Bounds.Width, ny * Bounds.Height);
    }

    private static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;
}
