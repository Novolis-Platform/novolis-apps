using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DraftStudio.Core;
using DraftStudio.Models;
using DraftStudio.Services;

namespace DraftStudio.Ui;

/// <summary>Plan-view (XZ) drafting canvas with pan, zoom, grid, snap, and hit-test.</summary>
internal sealed class DraftViewport : Control
{
    private readonly DraftSession _session;
    private readonly DraftSettingsStore _settings;
    private readonly DraftCommandDispatcher _dispatcher;
    private readonly ToolController _tools;
    private readonly IBrush _canvasBrush = new SolidColorBrush(Color.FromRgb(24, 26, 30));

    private double _scale = 40;
    private double _originX;
    private double _originZ;
    private bool _panning;
    private Point _lastPointer;
    private Point? _hoverScreen;

    public DraftViewport(
        DraftSession session,
        DraftSettingsStore settings,
        DraftCommandDispatcher dispatcher,
        ToolController tools)
    {
        _session = session;
        _settings = settings;
        _dispatcher = dispatcher;
        _tools = tools;
        Focusable = true;
        ClipToBounds = true;

        _session.Changed += () => InvalidateVisual();
        _tools.Changed += () => InvalidateVisual();
        _dispatcher.ToolChanged += () => InvalidateVisual();
    }

    public void Fit()
    {
        var bounds = EntityBounds.Compute(_session.Document);
        if (bounds.Radius < 0.01f)
            bounds = (new System.Numerics.Vector3(0, 0, 0), 5f);

        var w = Math.Max(1, Bounds.Width);
        var h = Math.Max(1, Bounds.Height);
        _scale = Math.Min(w, h) / (bounds.Radius * 2.5);
        _scale = Math.Clamp(_scale, 4, 400);
        _originX = bounds.Center.X;
        _originZ = bounds.Center.Z;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _panning = true;
            _lastPointer = p;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            var world = ScreenToWorld(p);
            world = Snap(world);
            if (_dispatcher.ActiveTool == DraftToolKind.Select)
            {
                var hit = HitTest(world);
                _session.SelectedId = hit?.Id;
                _session.Notify();
            }
            else
            {
                _tools.OnClick(world);
            }

            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _hoverScreen = p;
        if (_panning)
        {
            var dx = p.X - _lastPointer.X;
            var dy = p.Y - _lastPointer.Y;
            _originX -= dx / _scale;
            _originZ -= dy / _scale;
            _lastPointer = p;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var world = Snap(ScreenToWorld(p));
        _tools.OnHover(world);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var factor = e.Delta.Y > 0 ? 1.1 : 0.9;
        var before = ScreenToWorld(e.GetPosition(this));
        _scale = Math.Clamp(_scale * factor, 4, 400);
        var after = ScreenToWorld(e.GetPosition(this));
        _originX += before.X - after.X;
        _originZ += before.Z - after.Z;
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        context.FillRectangle(_canvasBrush, rect);
        DrawGrid(context);
        foreach (var entity in _session.Document.Entities)
            DrawEntity(context, entity, entity.Id == _session.SelectedId);
        _tools.DrawPreview(context, WorldToScreen);

        if (_hoverScreen is { } hp)
        {
            var w = Snap(ScreenToWorld(hp));
            var label = $"{w.X:0.##}, {w.Z:0.##}";
            context.DrawText(
                new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    Brushes.LightGray),
                new Point(8, Bounds.Height - 22));
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        var step = Math.Max(0.1f, _settings.Settings.GridStep);
        var majorEvery = 5;
        var topLeft = ScreenToWorld(new Point(0, 0));
        var bottomRight = ScreenToWorld(new Point(Bounds.Width, Bounds.Height));
        var minX = Math.Min(topLeft.X, bottomRight.X);
        var maxX = Math.Max(topLeft.X, bottomRight.X);
        var minZ = Math.Min(topLeft.Z, bottomRight.Z);
        var maxZ = Math.Max(topLeft.Z, bottomRight.Z);

        var startX = Math.Floor(minX / step) * step;
        var startZ = Math.Floor(minZ / step) * step;
        var minor = new Pen(new SolidColorBrush(Color.FromRgb(40, 44, 50)), 1);
        var major = new Pen(new SolidColorBrush(Color.FromRgb(55, 60, 70)), 1);
        var axis = new Pen(new SolidColorBrush(Color.FromRgb(90, 120, 160)), 1.5);

        for (var x = startX; x <= maxX + step; x += step)
        {
            var i = (int)Math.Round(x / step);
            var pen = Math.Abs(x) < step * 0.01 ? axis : (i % majorEvery == 0 ? major : minor);
            var a = WorldToScreen(new System.Numerics.Vector3((float)x, 0, (float)minZ));
            var b = WorldToScreen(new System.Numerics.Vector3((float)x, 0, (float)maxZ));
            context.DrawLine(pen, a, b);
        }

        for (var z = startZ; z <= maxZ + step; z += step)
        {
            var i = (int)Math.Round(z / step);
            var pen = Math.Abs(z) < step * 0.01 ? axis : (i % majorEvery == 0 ? major : minor);
            var a = WorldToScreen(new System.Numerics.Vector3((float)minX, 0, (float)z));
            var b = WorldToScreen(new System.Numerics.Vector3((float)maxX, 0, (float)z));
            context.DrawLine(pen, a, b);
        }
    }

    private void DrawEntity(DrawingContext context, CadEntity entity, bool selected)
    {
        var color = ToBrush(entity.Color ?? entity.Style?.Color, selected ? 1f : 0.9f);
        var pen = new Pen(color, selected ? 2.5 : 1.5);
        switch (entity.Kind.ToLowerInvariant())
        {
            case "line" when entity.A is not null && entity.B is not null:
                context.DrawLine(pen, WorldToScreen(CadVec.To(entity.A)), WorldToScreen(CadVec.To(entity.B)));
                break;
            case "circle" when entity.Center is not null:
            {
                var c = WorldToScreen(CadVec.To(entity.Center));
                var r = entity.Radius * _scale;
                context.DrawEllipse(null, pen, c, r, r);
                break;
            }
            case "rect" when entity.A is not null && entity.B is not null:
            {
                var a = CadVec.To(entity.A);
                var b = CadVec.To(entity.B);
                var p0 = WorldToScreen(a);
                var p1 = WorldToScreen(new System.Numerics.Vector3(b.X, 0, a.Z));
                var p2 = WorldToScreen(b);
                var p3 = WorldToScreen(new System.Numerics.Vector3(a.X, 0, b.Z));
                context.DrawLine(pen, p0, p1);
                context.DrawLine(pen, p1, p2);
                context.DrawLine(pen, p2, p3);
                context.DrawLine(pen, p3, p0);
                break;
            }
            case "spline" when entity.ControlPoints is { Count: >= 2 } && entity.Knots is not null:
            {
                var degree = entity.Degree <= 0 ? 3 : entity.Degree;
                var cps = entity.ControlPoints.Select(p => CadVec.To(p)).ToArray();
                var samples = Novolis.Math.Geometry.NurbsCurve.Tessellate(degree, cps, entity.Knots, entity.Weights, 64);
                for (var i = 1; i < samples.Length; i++)
                    context.DrawLine(pen, WorldToScreen(samples[i - 1]), WorldToScreen(samples[i]));
                break;
            }
            case "box" or "cylinder" or "sphere" or "cone" or "wedge" when entity.Center is not null:
            {
                var c = WorldToScreen(CadVec.To(entity.Center));
                var extent = entity.Kind == "sphere" || entity.Kind == "cylinder" || entity.Kind == "cone"
                    ? entity.Radius
                    : entity.HalfExtents is { Length: >= 1 } ? entity.HalfExtents[0] : 0.5f;
                var r = extent * _scale;
                context.DrawEllipse(null, new Pen(Brushes.Orange, selected ? 2 : 1), c, r, r * 0.6);
                break;
            }
        }
    }

    private CadEntity? HitTest(System.Numerics.Vector3 world)
    {
        var thresh = (float)(8 / _scale);
        CadEntity? best = null;
        var bestDist = float.MaxValue;
        foreach (var entity in _session.Document.Entities)
        {
            var d = DistanceToEntity(entity, world);
            if (d < thresh && d < bestDist)
            {
                bestDist = d;
                best = entity;
            }
        }

        return best;
    }

    private static float DistanceToEntity(CadEntity entity, System.Numerics.Vector3 p)
    {
        return entity.Kind.ToLowerInvariant() switch
        {
            "line" when entity.A is not null && entity.B is not null =>
                DistPointSegment(p, CadVec.To(entity.A), CadVec.To(entity.B)),
            "circle" when entity.Center is not null =>
                Math.Abs(System.Numerics.Vector3.Distance(
                    new System.Numerics.Vector3(p.X, 0, p.Z),
                    CadVec.To(entity.Center)) - entity.Radius),
            "rect" when entity.A is not null && entity.B is not null =>
                DistToRect(p, CadVec.To(entity.A), CadVec.To(entity.B)),
            "spline" => CadVec.EnumerateWorldPoints(entity)
                .Select(s => System.Numerics.Vector3.Distance(new System.Numerics.Vector3(p.X, 0, p.Z), new System.Numerics.Vector3(s.X, 0, s.Z)))
                .DefaultIfEmpty(float.MaxValue)
                .Min(),
            "box" or "cylinder" or "sphere" or "cone" or "wedge" when entity.Center is not null =>
                System.Numerics.Vector3.Distance(
                    new System.Numerics.Vector3(p.X, 0, p.Z),
                    new System.Numerics.Vector3(CadVec.To(entity.Center).X, 0, CadVec.To(entity.Center).Z)),
            _ => float.MaxValue,
        };
    }

    private static float DistToRect(System.Numerics.Vector3 p, System.Numerics.Vector3 a, System.Numerics.Vector3 b)
    {
        var c0 = a;
        var c1 = new System.Numerics.Vector3(b.X, 0, a.Z);
        var c2 = b;
        var c3 = new System.Numerics.Vector3(a.X, 0, b.Z);
        return Math.Min(
            Math.Min(DistPointSegment(p, c0, c1), DistPointSegment(p, c1, c2)),
            Math.Min(DistPointSegment(p, c2, c3), DistPointSegment(p, c3, c0)));
    }

    private static float DistPointSegment(System.Numerics.Vector3 p, System.Numerics.Vector3 a, System.Numerics.Vector3 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-8f ? 0f : System.Numerics.Vector3.Dot(p - a, ab) / ab.LengthSquared();
        t = Math.Clamp(t, 0f, 1f);
        return System.Numerics.Vector3.Distance(p, a + ab * t);
    }

    private System.Numerics.Vector3 Snap(System.Numerics.Vector3 p)
    {
        if (!_settings.Settings.SnapToGrid)
            return new System.Numerics.Vector3(p.X, 0, p.Z);
        var step = Math.Max(0.01f, _settings.Settings.GridStep);
        return new System.Numerics.Vector3(
            MathF.Round(p.X / step) * step,
            0,
            MathF.Round(p.Z / step) * step);
    }

    private System.Numerics.Vector3 ScreenToWorld(Point screen)
    {
        var x = (screen.X - Bounds.Width * 0.5) / _scale + _originX;
        var z = (screen.Y - Bounds.Height * 0.5) / _scale + _originZ;
        return new System.Numerics.Vector3((float)x, 0, (float)z);
    }

    private Point WorldToScreen(System.Numerics.Vector3 world) =>
        new(
            (world.X - _originX) * _scale + Bounds.Width * 0.5,
            (world.Z - _originZ) * _scale + Bounds.Height * 0.5);

    private static IBrush ToBrush(float[]? rgb, float a)
    {
        var r = (byte)Math.Clamp((int)((rgb is { Length: > 0 } ? rgb[0] : 0.85f) * 255), 0, 255);
        var g = (byte)Math.Clamp((int)((rgb is { Length: > 1 } ? rgb[1] : 0.85f) * 255), 0, 255);
        var b = (byte)Math.Clamp((int)((rgb is { Length: > 2 } ? rgb[2] : 0.9f) * 255), 0, 255);
        return new SolidColorBrush(Color.FromArgb((byte)(a * 255), r, g, b));
    }
}
