using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DraftStudio.Commands;
using DraftStudio.Core;
using DraftStudio.Models;
using DraftStudio.Services;
using System.Numerics;

namespace DraftStudio.Ui;

/// <summary>Plan-view (XZ) drafting canvas with pan, zoom, grid, snap, grips, and hit-test.</summary>
internal sealed class DraftViewport : Control
{
    private readonly DraftSession _session;
    private readonly DraftSettingsStore _settings;
    private readonly DraftCommandDispatcher _dispatcher;
    private readonly DraftCommandBus _bus;
    private readonly ToolController _tools;
    private readonly IBrush _canvasBrush = new SolidColorBrush(Color.FromRgb(24, 26, 30));

    private double _scale = 40;
    private double _originX;
    private double _originZ;
    private bool _panning;
    private Point _lastPointer;
    private Point? _hoverScreen;

    private GripKind? _activeGrip;
    private EntityGeometrySnapshot? _gripBefore;
    private CadEntity? _gripEntity;

    public DraftViewport(
        DraftSession session,
        DraftSettingsStore settings,
        DraftCommandDispatcher dispatcher,
        DraftCommandBus bus,
        ToolController tools)
    {
        _session = session;
        _settings = settings;
        _dispatcher = dispatcher;
        _bus = bus;
        _tools = tools;
        Focusable = true;
        ClipToBounds = true;

        _session.Changed += () => InvalidateVisual();
        _tools.Changed += () => InvalidateVisual();
        _dispatcher.ToolChanged += () => InvalidateVisual();
    }

    public double PixelsPerMeter => _scale;

    public event Action? ViewChanged;

    public void Fit()
    {
        var bounds = EntityBounds.Compute(_session.Document);
        if (bounds.Radius < 0.01f)
            bounds = (new Vector3(0, 0, 0), 5f);

        var w = Math.Max(1, Bounds.Width);
        var h = Math.Max(1, Bounds.Height);
        _scale = Math.Min(w, h) / (bounds.Radius * 2.5);
        _scale = Math.Clamp(_scale, 4, 400);
        _originX = bounds.Center.X;
        _originZ = bounds.Center.Z;
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void ZoomBy(double factor)
    {
        var center = new Point(Bounds.Width * 0.5, Bounds.Height * 0.5);
        var before = ScreenToWorld(center);
        _scale = Math.Clamp(_scale * factor, 4, 400);
        var after = ScreenToWorld(center);
        _originX += before.X - after.X;
        _originZ += before.Z - after.Z;
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void PanByPixels(double dx, double dy)
    {
        _originX -= dx / _scale;
        _originZ -= dy / _scale;
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void ResetView()
    {
        _originX = 0;
        _originZ = 0;
        _scale = 40;
        InvalidateVisual();
        ViewChanged?.Invoke();
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
            var world = Snap(ScreenToWorld(p));
            if (_dispatcher.ActiveTool == DraftToolKind.Select)
            {
                if (TryBeginGrip(world))
                {
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }

                var hit = HitTest(world);
                _session.SelectedId = hit?.Id;
                _session.Notify();
            }
            else
            {
                _tools.OnClick(world, (float)_scale);
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
            ViewChanged?.Invoke();
            e.Handled = true;
            return;
        }

        if (_activeGrip is not null && _gripEntity is not null)
        {
            ApplyGrip(Snap(ScreenToWorld(p)));
            _session.Notify();
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
            return;
        }

        if (_activeGrip is not null && _gripEntity is not null && _gripBefore is not null)
        {
            var after = EntityGeometrySnapshot.Capture(_gripEntity);
            _bus.Execute(new MutateEntityGeometryCommand(_gripEntity.Id, _gripBefore, after));
            _activeGrip = null;
            _gripBefore = null;
            _gripEntity = null;
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
        ViewChanged?.Invoke();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        context.FillRectangle(_canvasBrush, rect);
        DrawGrid(context);
        foreach (var entity in _session.Document.Entities)
        {
            var onLevel = IsOnLevel(entity);
            if (_settings.Settings.IsolateLevel && !onLevel && !entity.IsSolid)
            {
                DrawEntity(context, entity, selected: false, dimmed: true);
                continue;
            }

            DrawEntity(context, entity, entity.Id == _session.SelectedId, dimmed: !onLevel);
        }

        _tools.DrawPreview(context, WorldToScreen, _scale);
        DrawGrips(context);
        DrawScaleBar(context);
        DrawLevelBadge(context);

        if (_hoverScreen is { } hp)
        {
            var w = Snap(ScreenToWorld(hp));
            var unit = _settings.Settings.DisplayUnit;
            var elev = DraftUnits.FormatLength(_settings.Settings.DrawElevation, unit);
            var label =
                $"{DraftUnits.ToDisplay(w.X, unit):0.##}, {DraftUnits.ToDisplay(w.Z, unit):0.##} {DraftUnits.Abbreviation(unit)}  ·  L={elev}";
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

    private void DrawLevelBadge(DrawingContext context)
    {
        var unit = _settings.Settings.DisplayUnit;
        var text = $"Level {DraftUnits.FormatLength(_settings.Settings.DrawElevation, unit)}";
        context.DrawText(
            new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                new SolidColorBrush(Color.FromRgb(180, 200, 230))),
            new Point(Bounds.Width - 140, 10));
    }

    private void DrawScaleBar(DrawingContext context)
    {
        if (_scale <= 0 || Bounds.Width < 80)
            return;

        var unit = _settings.Settings.DisplayUnit;
        var metersPerPixel = 1.0 / _scale;
        var (meters, label) = DraftUnits.NiceScaleBar(metersPerPixel, unit);
        var barPx = meters * _scale;
        if (barPx < 24 || barPx > Bounds.Width * 0.45)
            return;

        var y = Bounds.Height - 40;
        var x1 = 12.0;
        var x2 = x1 + barPx;
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(200, 205, 215)), 1.5);
        context.DrawLine(pen, new Point(x1, y), new Point(x2, y));
        context.DrawLine(pen, new Point(x1, y - 5), new Point(x1, y + 5));
        context.DrawLine(pen, new Point(x2, y - 5), new Point(x2, y + 5));
        context.DrawText(
            new FormattedText(
                label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                Brushes.LightGray),
            new Point(x1, y - 20));
    }

    private void DrawGrid(DrawingContext context)
    {
        var step = Math.Max(0.05f, _settings.Settings.GridStep);
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
            var a = WorldToScreen(new Vector3((float)x, 0, (float)minZ));
            var b = WorldToScreen(new Vector3((float)x, 0, (float)maxZ));
            context.DrawLine(pen, a, b);
        }

        for (var z = startZ; z <= maxZ + step; z += step)
        {
            var i = (int)Math.Round(z / step);
            var pen = Math.Abs(z) < step * 0.01 ? axis : (i % majorEvery == 0 ? major : minor);
            var a = WorldToScreen(new Vector3((float)minX, 0, (float)z));
            var b = WorldToScreen(new Vector3((float)maxX, 0, (float)z));
            context.DrawLine(pen, a, b);
        }
    }

    private void DrawEntity(DrawingContext context, CadEntity entity, bool selected, bool dimmed)
    {
        var alpha = dimmed ? 0.28f : selected ? 1f : 0.9f;
        var color = ToBrush(entity.Color ?? entity.Style?.Color, alpha);
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
                DrawRectFootprint(context, pen, CadVec.To(entity.A), CadVec.To(entity.B));
                break;
            case "spline" when entity.ControlPoints is { Count: >= 2 } && entity.Knots is not null:
            {
                var degree = entity.Degree <= 0 ? 3 : entity.Degree;
                var cps = entity.ControlPoints.Select(p => CadVec.To(p)).ToArray();
                var samples = Novolis.Math.Geometry.NurbsCurve.Tessellate(degree, cps, entity.Knots, entity.Weights, 64);
                for (var i = 1; i < samples.Length; i++)
                    context.DrawLine(pen, WorldToScreen(samples[i - 1]), WorldToScreen(samples[i]));
                break;
            }
            case "box" when entity.Center is not null:
            {
                var center = CadVec.To(entity.Center);
                var hx = entity.HalfExtents is { Length: >= 1 } ? entity.HalfExtents[0] : 0.5f;
                var hz = entity.HalfExtents is { Length: >= 3 } ? entity.HalfExtents[2] : hx;
                var solidPen = SolidFootprintPen(selected, dimmed);
                DrawRectFootprint(
                    context,
                    solidPen,
                    new Vector3(center.X - hx, 0, center.Z - hz),
                    new Vector3(center.X + hx, 0, center.Z + hz));
                DrawSolidMarker(context, WorldToScreen(center), selected);
                break;
            }
            case "cylinder" or "cone" when entity.Center is not null:
            {
                var c = WorldToScreen(CadVec.To(entity.Center));
                var r = entity.Radius * _scale;
                context.DrawEllipse(null, SolidFootprintPen(selected, dimmed), c, r, r);
                DrawSolidMarker(context, c, selected);
                break;
            }
            case "sphere" when entity.Center is not null:
            {
                var c = WorldToScreen(CadVec.To(entity.Center));
                var r = entity.Radius * _scale;
                context.DrawEllipse(null, SolidFootprintPen(selected, dimmed), c, r, r);
                DrawSolidMarker(context, c, selected);
                break;
            }
            case "wedge" when entity.Center is not null:
            {
                var center = CadVec.To(entity.Center);
                var hx = entity.HalfExtents is { Length: >= 1 } ? entity.HalfExtents[0] : 0.5f;
                var hz = entity.HalfExtents is { Length: >= 3 } ? entity.HalfExtents[2] : hx;
                DrawRectFootprint(
                    context,
                    SolidFootprintPen(selected, dimmed),
                    new Vector3(center.X - hx, 0, center.Z - hz),
                    new Vector3(center.X + hx, 0, center.Z + hz));
                DrawSolidMarker(context, WorldToScreen(center), selected);
                break;
            }
        }
    }

    private void DrawGrips(DrawingContext context)
    {
        if (_dispatcher.ActiveTool != DraftToolKind.Select)
            return;
        var selected = _session.SelectedEntity;
        if (selected is null)
            return;

        foreach (var grip in EnumerateGrips(selected))
        {
            var s = WorldToScreen(grip.World);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(40, 40, 45)),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 90)), 1.5),
                new Rect(s.X - 5, s.Y - 5, 10, 10));
        }
    }

    private bool TryBeginGrip(Vector3 world)
    {
        var selected = _session.SelectedEntity;
        if (selected is null)
            return false;

        var thresh = (float)(10 / _scale);
        foreach (var grip in EnumerateGrips(selected))
        {
            var d = Vector3.Distance(
                new Vector3(world.X, 0, world.Z),
                new Vector3(grip.World.X, 0, grip.World.Z));
            if (d <= thresh)
            {
                _activeGrip = grip.Kind;
                _gripEntity = selected;
                _gripBefore = EntityGeometrySnapshot.Capture(selected);
                return true;
            }
        }

        return false;
    }

    private void ApplyGrip(Vector3 world)
    {
        if (_gripEntity is null || _activeGrip is null)
            return;

        var elev = _settings.Settings.DrawElevation;
        switch (_gripEntity.Kind.ToLowerInvariant())
        {
            case "line":
                if (_activeGrip == GripKind.LineA)
                    _gripEntity.A = CadVec.Plan(world.X, world.Z, CadVec.To(_gripEntity.A).Y);
                else if (_activeGrip == GripKind.LineB)
                    _gripEntity.B = CadVec.Plan(world.X, world.Z, CadVec.To(_gripEntity.B).Y);
                break;

            case "box" when _gripEntity.Center is not null && _gripEntity.HalfExtents is { Length: >= 3 }:
            {
                var c = CadVec.To(_gripEntity.Center);
                var he = _gripEntity.HalfExtents;
                switch (_activeGrip)
                {
                    case GripKind.BoxMinX:
                        {
                            var maxX = c.X + he[0];
                            var newHx = Math.Max(0.05f, (maxX - world.X) * 0.5f);
                            var newCx = maxX - newHx;
                            he[0] = newHx;
                            _gripEntity.Center = CadVec.Xyz(newCx, c.Y, c.Z);
                            break;
                        }
                    case GripKind.BoxMaxX:
                        {
                            var minX = c.X - he[0];
                            var newHx = Math.Max(0.05f, (world.X - minX) * 0.5f);
                            var newCx = minX + newHx;
                            he[0] = newHx;
                            _gripEntity.Center = CadVec.Xyz(newCx, c.Y, c.Z);
                            break;
                        }
                    case GripKind.BoxMinZ:
                        {
                            var maxZ = c.Z + he[2];
                            var newHz = Math.Max(0.05f, (maxZ - world.Z) * 0.5f);
                            var newCz = maxZ - newHz;
                            he[2] = newHz;
                            _gripEntity.Center = CadVec.Xyz(c.X, c.Y, newCz);
                            break;
                        }
                    case GripKind.BoxMaxZ:
                        {
                            var minZ = c.Z - he[2];
                            var newHz = Math.Max(0.05f, (world.Z - minZ) * 0.5f);
                            var newCz = minZ + newHz;
                            he[2] = newHz;
                            _gripEntity.Center = CadVec.Xyz(c.X, c.Y, newCz);
                            break;
                        }
                }

                break;
            }

            case "circle" when _gripEntity.Center is not null && _activeGrip == GripKind.CircleRadius:
            {
                var c = CadVec.To(_gripEntity.Center);
                _gripEntity.Radius = Math.Max(0.05f, Vector3.Distance(
                    new Vector3(c.X, 0, c.Z),
                    new Vector3(world.X, 0, world.Z)));
                break;
            }

            case "rect" when _gripEntity.A is not null && _gripEntity.B is not null:
            {
                var a = CadVec.To(_gripEntity.A);
                if (_activeGrip == GripKind.RectA)
                    _gripEntity.A = CadVec.Plan(world.X, world.Z, a.Y);
                else if (_activeGrip == GripKind.RectB)
                    _gripEntity.B = CadVec.Plan(world.X, world.Z, a.Y);
                break;
            }
        }

        _ = elev;
    }

    private static IEnumerable<(GripKind Kind, Vector3 World)> EnumerateGrips(CadEntity entity)
    {
        switch (entity.Kind.ToLowerInvariant())
        {
            case "line" when entity.A is not null && entity.B is not null:
                yield return (GripKind.LineA, CadVec.To(entity.A));
                yield return (GripKind.LineB, CadVec.To(entity.B));
                break;
            case "box" when entity.Center is not null && entity.HalfExtents is { Length: >= 3 }:
            {
                var c = CadVec.To(entity.Center);
                var hx = entity.HalfExtents[0];
                var hz = entity.HalfExtents[2];
                yield return (GripKind.BoxMinX, new Vector3(c.X - hx, c.Y, c.Z));
                yield return (GripKind.BoxMaxX, new Vector3(c.X + hx, c.Y, c.Z));
                yield return (GripKind.BoxMinZ, new Vector3(c.X, c.Y, c.Z - hz));
                yield return (GripKind.BoxMaxZ, new Vector3(c.X, c.Y, c.Z + hz));
                break;
            }
            case "circle" when entity.Center is not null:
            {
                var c = CadVec.To(entity.Center);
                yield return (GripKind.CircleRadius, c + new Vector3(entity.Radius, 0, 0));
                break;
            }
            case "rect" when entity.A is not null && entity.B is not null:
                yield return (GripKind.RectA, CadVec.To(entity.A));
                yield return (GripKind.RectB, CadVec.To(entity.B));
                break;
        }
    }

    private void DrawRectFootprint(DrawingContext context, IPen pen, Vector3 a, Vector3 b)
    {
        var p0 = WorldToScreen(a);
        var p1 = WorldToScreen(new Vector3(b.X, 0, a.Z));
        var p2 = WorldToScreen(b);
        var p3 = WorldToScreen(new Vector3(a.X, 0, b.Z));
        context.DrawLine(pen, p0, p1);
        context.DrawLine(pen, p1, p2);
        context.DrawLine(pen, p2, p3);
        context.DrawLine(pen, p3, p0);
    }

    private static Pen SolidFootprintPen(bool selected, bool dimmed = false)
    {
        var a = dimmed ? (byte)70 : (byte)255;
        return new Pen(new SolidColorBrush(Color.FromArgb(
            a,
            (byte)(selected ? 255 : 210),
            (byte)(selected ? 170 : 140),
            70)), selected ? 2 : 1.5)
        {
            DashStyle = new DashStyle([4.0, 3.0], 0),
        };
    }

    private void DrawSolidMarker(DrawingContext context, Point center, bool selected)
    {
        var brush = selected
            ? new SolidColorBrush(Color.FromRgb(255, 190, 90))
            : new SolidColorBrush(Color.FromRgb(180, 130, 60));
        context.DrawEllipse(brush, null, center, 3, 3);
    }

    private bool IsOnLevel(CadEntity entity) =>
        CadVec.MatchesLevel(entity, _settings.Settings.DrawElevation, _settings.Settings.LevelTolerance);

    private CadEntity? HitTest(Vector3 world)
    {
        var thresh = (float)(8 / _scale);
        CadEntity? best = null;
        var bestDist = float.MaxValue;
        foreach (var entity in _session.Document.Entities)
        {
            if (_settings.Settings.IsolateLevel && !IsOnLevel(entity) && !entity.IsSolid)
                continue;

            var d = DistanceToEntity(entity, world);
            if (d < thresh && d < bestDist)
            {
                bestDist = d;
                best = entity;
            }
        }

        return best;
    }

    private static float DistanceToEntity(CadEntity entity, Vector3 p)
    {
        return entity.Kind.ToLowerInvariant() switch
        {
            "line" when entity.A is not null && entity.B is not null =>
                DistPointSegment(p, CadVec.To(entity.A), CadVec.To(entity.B)),
            "circle" when entity.Center is not null =>
                Math.Abs(Vector3.Distance(
                    new Vector3(p.X, 0, p.Z),
                    new Vector3(CadVec.To(entity.Center).X, 0, CadVec.To(entity.Center).Z)) - entity.Radius),
            "rect" when entity.A is not null && entity.B is not null =>
                DistToRect(p, CadVec.To(entity.A), CadVec.To(entity.B)),
            "spline" => CadVec.EnumerateWorldPoints(entity)
                .Select(s => Vector3.Distance(new Vector3(p.X, 0, p.Z), new Vector3(s.X, 0, s.Z)))
                .DefaultIfEmpty(float.MaxValue)
                .Min(),
            "box" or "wedge" when entity.Center is not null =>
                DistToBoxFootprint(p, CadVec.To(entity.Center), entity.HalfExtents),
            "cylinder" or "cone" or "sphere" when entity.Center is not null =>
                Math.Abs(Vector3.Distance(
                    new Vector3(p.X, 0, p.Z),
                    new Vector3(CadVec.To(entity.Center).X, 0, CadVec.To(entity.Center).Z)) - entity.Radius),
            _ => float.MaxValue,
        };
    }

    private static float DistToBoxFootprint(Vector3 p, Vector3 center, float[]? halfExtents)
    {
        var hx = halfExtents is { Length: >= 1 } ? halfExtents[0] : 0.5f;
        var hz = halfExtents is { Length: >= 3 } ? halfExtents[2] : hx;
        return DistToRect(
            p,
            new Vector3(center.X - hx, 0, center.Z - hz),
            new Vector3(center.X + hx, 0, center.Z + hz));
    }

    private static float DistToRect(Vector3 p, Vector3 a, Vector3 b)
    {
        var c0 = a;
        var c1 = new Vector3(b.X, 0, a.Z);
        var c2 = b;
        var c3 = new Vector3(a.X, 0, b.Z);
        return Math.Min(
            Math.Min(DistPointSegment(p, c0, c1), DistPointSegment(p, c1, c2)),
            Math.Min(DistPointSegment(p, c2, c3), DistPointSegment(p, c3, c0)));
    }

    private static float DistPointSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-8f ? 0f : Vector3.Dot(p - a, ab) / ab.LengthSquared();
        t = Math.Clamp(t, 0f, 1f);
        return Vector3.Distance(
            new Vector3(p.X, 0, p.Z),
            new Vector3((a + ab * t).X, 0, (a + ab * t).Z));
    }

    private Vector3 Snap(Vector3 p)
    {
        if (!_settings.Settings.SnapToGrid)
            return new Vector3(p.X, _settings.Settings.DrawElevation, p.Z);
        var step = Math.Max(0.01f, _settings.Settings.GridStep);
        return new Vector3(
            MathF.Round(p.X / step) * step,
            _settings.Settings.DrawElevation,
            MathF.Round(p.Z / step) * step);
    }

    private Vector3 ScreenToWorld(Point screen)
    {
        var x = (screen.X - Bounds.Width * 0.5) / _scale + _originX;
        var z = (screen.Y - Bounds.Height * 0.5) / _scale + _originZ;
        return new Vector3((float)x, _settings.Settings.DrawElevation, (float)z);
    }

    private Point WorldToScreen(Vector3 world) =>
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

    private enum GripKind
    {
        LineA,
        LineB,
        BoxMinX,
        BoxMaxX,
        BoxMinZ,
        BoxMaxZ,
        CircleRadius,
        RectA,
        RectB,
    }
}
