using Avalonia;
using Avalonia.Media;
using DraftStudio.Models;
using DraftStudio.Services;
using Novolis.Math.Geometry;
using System.Numerics;

namespace DraftStudio.Ui;

internal sealed class ToolController
{
    private readonly DraftCommandDispatcher _dispatcher;
    private readonly List<Vector3> _points = [];
    private Vector3? _hover;

    public ToolController(DraftCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _dispatcher.ToolChanged += () =>
        {
            _points.Clear();
            _hover = null;
            Changed?.Invoke();
        };
    }

    public event Action? Changed;

    public string PromptHint => _dispatcher.ActiveTool switch
    {
        DraftToolKind.Line => _points.Count == 0 ? "Line: specify first point" : "Line: specify second point",
        DraftToolKind.Circle => _points.Count == 0 ? "Circle: specify center" : "Circle: specify radius point",
        DraftToolKind.Rect => _points.Count == 0 ? "Rect: specify first corner" : "Rect: specify opposite corner",
        DraftToolKind.Spline => _points.Count == 0
            ? "Spline: specify points (Enter to finish)"
            : $"Spline: {_points.Count} points — click more or Enter to finish",
        _ => "Command:",
    };

    public bool IsCollectingSpline => _dispatcher.ActiveTool == DraftToolKind.Spline && _points.Count > 0;

    public void Cancel()
    {
        _points.Clear();
        _hover = null;
        _dispatcher.EnterTool(DraftToolKind.Select);
        Changed?.Invoke();
    }

    public bool TryCommitSpline()
    {
        if (_dispatcher.ActiveTool != DraftToolKind.Spline || _points.Count < 2)
            return false;

        var (degree, controls, knots, weights) = NurbsCurve.FromFitPoints(_points);
        _dispatcher.EmitAdd(new CadEntity
        {
            Name = "Spline",
            Kind = "spline",
            Degree = degree,
            ControlPoints = controls.Select(CadVec.From).ToList(),
            Knots = knots,
            Weights = weights,
            FitPoints = _points.Select(CadVec.From).ToList(),
            Closed = false,
            Normal = [0f, 1f, 0f],
        });
        _points.Clear();
        Changed?.Invoke();
        return true;
    }

    public void OnHover(Vector3 world)
    {
        _hover = world;
        Changed?.Invoke();
    }

    public void OnClick(Vector3 world)
    {
        switch (_dispatcher.ActiveTool)
        {
            case DraftToolKind.Line:
                _points.Add(world);
                if (_points.Count >= 2)
                {
                    var a = _points[0];
                    var b = _points[1];
                    _dispatcher.EmitAdd(new CadEntity
                    {
                        Name = "Line",
                        Kind = "line",
                        A = CadVec.Xz(a.X, a.Z),
                        B = CadVec.Xz(b.X, b.Z),
                        Style = new CadStyle { Linetype = "Continuous" },
                    });
                    _points.Clear();
                }

                break;

            case DraftToolKind.Circle:
                _points.Add(world);
                if (_points.Count >= 2)
                {
                    var c = _points[0];
                    var r = Vector3.Distance(c, _points[1]);
                    _dispatcher.EmitAdd(new CadEntity
                    {
                        Name = "Circle",
                        Kind = "circle",
                        Center = CadVec.Xz(c.X, c.Z),
                        Radius = r,
                        Normal = [0f, 1f, 0f],
                    });
                    _points.Clear();
                }

                break;

            case DraftToolKind.Rect:
                _points.Add(world);
                if (_points.Count >= 2)
                {
                    var a = _points[0];
                    var b = _points[1];
                    _dispatcher.EmitAdd(new CadEntity
                    {
                        Name = "Rect",
                        Kind = "rect",
                        A = CadVec.Xz(a.X, a.Z),
                        B = CadVec.Xz(b.X, b.Z),
                        Normal = [0f, 1f, 0f],
                    });
                    _points.Clear();
                }

                break;

            case DraftToolKind.Spline:
                _points.Add(world);
                break;
        }

        Changed?.Invoke();
    }

    public void DrawPreview(DrawingContext context, Func<Vector3, Point> worldToScreen)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 120, 200, 255)), 1.5, dashStyle: DashStyle.Dash);

        if (_dispatcher.ActiveTool == DraftToolKind.Spline && _points.Count > 0)
        {
            var preview = _hover is { } h ? _points.Append(h).ToList() : _points;
            if (preview.Count >= 2)
            {
                var (degree, controls, knots, weights) = NurbsCurve.FromFitPoints(preview);
                var samples = NurbsCurve.Tessellate(degree, controls, knots, weights, 48);
                for (var i = 1; i < samples.Length; i++)
                    context.DrawLine(pen, worldToScreen(samples[i - 1]), worldToScreen(samples[i]));
            }

            foreach (var p in _points)
            {
                var s = worldToScreen(p);
                context.DrawEllipse(Brushes.DeepSkyBlue, null, s, 3, 3);
            }

            return;
        }

        if (_points.Count == 0 || _hover is null)
            return;

        var a = _points[0];
        var b = _hover.Value;
        switch (_dispatcher.ActiveTool)
        {
            case DraftToolKind.Line:
                context.DrawLine(pen, worldToScreen(a), worldToScreen(b));
                break;
            case DraftToolKind.Circle:
            {
                var c = worldToScreen(a);
                var r = Vector3.Distance(a, b) * EstimateScale(worldToScreen, a);
                context.DrawEllipse(null, pen, c, r, r);
                break;
            }
            case DraftToolKind.Rect:
            {
                var p0 = worldToScreen(a);
                var p1 = worldToScreen(new Vector3(b.X, 0, a.Z));
                var p2 = worldToScreen(b);
                var p3 = worldToScreen(new Vector3(a.X, 0, b.Z));
                context.DrawLine(pen, p0, p1);
                context.DrawLine(pen, p1, p2);
                context.DrawLine(pen, p2, p3);
                context.DrawLine(pen, p3, p0);
                break;
            }
        }
    }

    private static double EstimateScale(Func<Vector3, Point> worldToScreen, Vector3 origin)
    {
        var a = worldToScreen(origin);
        var b = worldToScreen(origin + new Vector3(1, 0, 0));
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
    }
}
