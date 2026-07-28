using System.Drawing;
using System.Numerics;
using DraftStudio.Core;
using DraftStudio.Models;
using Novolis.Avalonia.Raylib;
using Novolis.Math.Geometry;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Rendering.Presentation.Silk;

namespace DraftStudio.Services;

internal sealed class DraftModelRenderer
{
    private static readonly Color Background = Color.FromArgb(255, 28, 28, 32);
    private static readonly Color GridMajor = Color.FromArgb(255, 48, 52, 58);
    private static readonly Color GridMinor = Color.FromArgb(255, 36, 38, 42);
    private static readonly Color Sketch = Color.FromArgb(255, 180, 200, 230);
    private static readonly Color Solid = Color.FromArgb(255, 120, 150, 170);
    private static readonly Color Selected = Color.FromArgb(255, 255, 200, 80);
    private static readonly Color Hud = Color.FromArgb(255, 180, 180, 190);

    private readonly DraftSession _session;
    private readonly SilkOrbitCamera _orbit = new()
    {
        Target = new Vector3(0f, 0.5f, 0f),
        Distance = 24f,
        MinDistance = 2f,
        MaxDistance = 200f,
        Yaw = 0.9f,
        Pitch = 0.45f,
    };

    public DraftModelRenderer(DraftSession session) => _session = session;

    public SilkOrbitCamera Orbit => _orbit;

    public void Bind(RaylibHostControl host) =>
        host.FrameRendering += (_, e) => OnFrame(e.DeltaSeconds, e.ScreenWidth, e.ScreenHeight);

    public void Fit()
    {
        var (center, radius) = EntityBounds.Compute(_session.Document);
        _orbit.Target = center + new Vector3(0, Math.Max(0.5f, radius * 0.15f), 0);
        _orbit.Distance = Math.Clamp(Math.Max(6f, radius * 2.8f), _orbit.MinDistance, _orbit.MaxDistance);
    }

    public void OrbitDrag(float dx, float dy) =>
        _orbit.AddLookDelta(dx * 0.01f, dy * 0.01f);

    public void Zoom(float delta) =>
        _orbit.AdjustDistance(delta > 0 ? -1.5f : 1.5f);

    private void OnFrame(float deltaSeconds, int screenWidth, int screenHeight)
    {
        _ = deltaSeconds;
        _ = screenWidth;
        _ = screenHeight;
        Graphics.ClearBackground(Background);
        var eye = _orbit.BuildEyePosition();
        var camera = Camera.Perspective(eye, _orbit.Target, Vector3.UnitY, _orbit.FieldOfViewDegrees);
        World.Begin(camera);
        World.DrawGrid(32, 1f);
        DrawGrid();
        foreach (var entity in _session.Document.Entities)
            DrawEntity(entity, entity.Id == _session.SelectedId);
        World.End();
        Graphics.DrawText($"Model — {_session.Document.Entities.Count} entities", 8, 8, 14, Hud);
    }

    private static void DrawGrid()
    {
        const float extent = 20f;
        const float step = 1f;
        for (float o = -extent; o <= extent; o += step)
        {
            var major = Math.Abs(o) < 0.01f || Math.Abs(o % 5f) < 0.01f;
            var c = major ? GridMajor : GridMinor;
            World.DrawLine(new Vector3(-extent, 0.01f, o), new Vector3(extent, 0.01f, o), c);
            World.DrawLine(new Vector3(o, 0.01f, -extent), new Vector3(o, 0.01f, extent), c);
        }
    }

    private static void DrawEntity(CadEntity entity, bool selected)
    {
        var color = selected ? Selected : (entity.IsSolid ? Solid : Sketch);
        switch (entity.Kind.ToLowerInvariant())
        {
            case "line" when entity.A is not null && entity.B is not null:
                World.DrawLine(CadVec.To(entity.A) + new Vector3(0, 0.02f, 0), CadVec.To(entity.B) + new Vector3(0, 0.02f, 0), color);
                break;
            case "circle" when entity.Center is not null:
            {
                var c = CadVec.To(entity.Center) with { Y = 0.02f };
                const int segments = 48;
                Vector3? prev = null;
                for (var i = 0; i <= segments; i++)
                {
                    var a = i * (MathF.PI * 2f / segments);
                    var p = c + new Vector3(MathF.Cos(a) * entity.Radius, 0, MathF.Sin(a) * entity.Radius);
                    if (prev is { } q)
                        World.DrawLine(q, p, color);
                    prev = p;
                }

                break;
            }
            case "rect" when entity.A is not null && entity.B is not null:
            {
                var a = CadVec.To(entity.A) with { Y = 0.02f };
                var b = CadVec.To(entity.B) with { Y = 0.02f };
                var p0 = a;
                var p1 = new Vector3(b.X, 0.02f, a.Z);
                var p2 = b;
                var p3 = new Vector3(a.X, 0.02f, b.Z);
                World.DrawLine(p0, p1, color);
                World.DrawLine(p1, p2, color);
                World.DrawLine(p2, p3, color);
                World.DrawLine(p3, p0, color);
                break;
            }
            case "spline" when entity.ControlPoints is { Count: >= 2 } && entity.Knots is not null:
            {
                var degree = entity.Degree <= 0 ? 3 : entity.Degree;
                var cps = entity.ControlPoints.Select(p => CadVec.To(p)).ToArray();
                var samples = NurbsCurve.Tessellate(degree, cps, entity.Knots, entity.Weights, 64);
                for (var i = 1; i < samples.Length; i++)
                {
                    var a = samples[i - 1] with { Y = 0.02f };
                    var b = samples[i] with { Y = 0.02f };
                    World.DrawLine(a, b, color);
                }

                break;
            }
            case "box" when entity.Center is not null && entity.HalfExtents is { Length: >= 3 }:
            {
                var he = CadVec.To(entity.HalfExtents);
                World.DrawCube(CadVec.To(entity.Center), he.X * 2f, he.Y * 2f, he.Z * 2f, color);
                break;
            }
            case "cylinder" when entity.Center is not null:
                World.DrawCylinder(
                    CadVec.To(entity.Center) - new Vector3(0, entity.Height * 0.5f, 0),
                    entity.Radius,
                    entity.Radius,
                    entity.Height,
                    24,
                    color);
                break;
            case "sphere" when entity.Center is not null:
                World.DrawSphere(CadVec.To(entity.Center), entity.Radius, color);
                break;
        }
    }
}
