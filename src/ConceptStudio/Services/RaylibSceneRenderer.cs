using System.Drawing;
using System.Numerics;
using ConceptStudio.Models;
using Novolis.Raylib.Abstractions;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Rendering.Presentation.Silk;

namespace ConceptStudio.Services;

internal sealed class RaylibSceneRenderer : IRaylibFrameRenderer
{
    private static readonly Color PreviewBackground = Color.FromArgb(255, 28, 28, 32);
    private static readonly Color WireColor = Color.FromArgb(255, 64, 64, 72);
    private static readonly Color EdgeColor = Color.FromArgb(255, 20, 20, 24);
    private static readonly Color HudColor = Color.FromArgb(255, 180, 180, 190);
    private static readonly Color GridMajor = Color.FromArgb(255, 48, 52, 58);
    private static readonly Color GridMinor = Color.FromArgb(255, 36, 38, 42);

    private Func<ConceptDocument> _getScene = () => new();
    private Func<SilkOrbitCamera> _getOrbit = () => new();
    private Func<ConceptViewMode> _getViewMode = () => ConceptViewMode.Orbit;
    private Func<bool> _getShowWireframe = () => true;
    private Func<bool> _getShowGrid = () => true;

    public void Bind(
        Func<ConceptDocument> getScene,
        Func<SilkOrbitCamera> getOrbit,
        Func<ConceptViewMode> getViewMode,
        Func<bool> getShowWireframe,
        Func<bool> getShowGrid)
    {
        _getScene = getScene;
        _getOrbit = getOrbit;
        _getViewMode = getViewMode;
        _getShowWireframe = getShowWireframe;
        _getShowGrid = getShowGrid;
    }

    public void OnFrame(float deltaSeconds, int screenWidth, int screenHeight)
    {
        _ = deltaSeconds;
        var scene = _getScene();
        var viewMode = _getViewMode();
        var (center, radius) = SceneBounds.Compute(scene);
        var camera = viewMode == ConceptViewMode.Orbit
            ? BuildOrbitCamera(_getOrbit(), screenWidth, screenHeight)
            : BuildOrthographicCamera(viewMode, center, radius, screenWidth, screenHeight);

        Graphics.ClearBackground(PreviewBackground);
        World.Begin(camera);

        if (_getShowGrid())
            DrawConceptGrid(viewMode, center, radius);

        foreach (var part in scene.Parts.Where(p => !p.IsGroup))
            DrawPart(part);

        World.End();
        var label = viewMode switch
        {
            ConceptViewMode.Plan => "Plan (top)",
            ConceptViewMode.Profile => "Profile (side)",
            ConceptViewMode.Bow => "Bow (front)",
            _ => "Orbit",
        };
        Graphics.DrawText($"{label} — {scene.Parts.Count(p => !p.IsGroup)} parts", 8, 8, 14, HudColor);
    }

    private void DrawPart(ConceptPartRecord part)
    {
        var center = ConceptMaterialMapper.ToVector3(part.Center);
        var color = ToColor(part.Color);
        var wire = _getShowWireframe();
        var scale = part.Scale.Length >= 3
            ? new Vector3(part.Scale[0], part.Scale[1], part.Scale[2])
            : Vector3.One;

        switch (part.Kind.ToLowerInvariant())
        {
            case "sphere":
                World.DrawSphere(center, part.Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)), color);
                if (wire)
                    World.DrawSphereWires(center, part.Radius, 8, 12, WireColor);
                break;
            case "cylinder":
            {
                var r = part.Radius * MathF.Max(scale.X, scale.Z);
                var h = part.Height * scale.Y;
                World.DrawCylinder(center, r, r, h, 24, color);
                if (wire)
                    World.DrawSphereWires(center, r, 4, 12, WireColor);
                break;
            }
            case "cone":
            {
                var r = part.Radius * MathF.Max(scale.X, scale.Z);
                var h = part.Height * scale.Y;
                World.DrawCylinder(center, 0.001f, r, h, 24, color);
                if (wire)
                    World.DrawSphereWires(center, r, 4, 12, WireColor);
                break;
            }
            case "wedge":
            {
                var size = ToVector3(part.HalfExtents) * 2f * scale;
                World.DrawCubeV(center, size, color);
                if (wire)
                    World.DrawCubeWiresV(center, size, EdgeColor);
                break;
            }
            default:
            {
                var size = ToVector3(part.HalfExtents) * 2f * scale;
                World.DrawCubeV(center, size, color);
                if (wire)
                    World.DrawCubeWiresV(center, size, EdgeColor);
                break;
            }
        }
    }

    private static void DrawConceptGrid(ConceptViewMode mode, Vector3 center, float radius)
    {
        if (mode == ConceptViewMode.Orbit)
        {
            World.DrawGrid(32, MathF.Max(1f, radius * 0.15f));
            return;
        }

        var extent = radius * 2.2f;
        var step = MathF.Max(1f, MathF.Round(extent / 16f));
        var count = (int)(extent / step);
        var origin = center;
        for (var i = -count; i <= count; i++)
        {
            var o = i * step;
            var major = i % 5 == 0;
            var c = major ? GridMajor : GridMinor;
            switch (mode)
            {
                case ConceptViewMode.Plan:
                    World.DrawLine(origin + new Vector3(-extent, 0.01f, o), origin + new Vector3(extent, 0.01f, o), c);
                    World.DrawLine(origin + new Vector3(o, 0.01f, -extent), origin + new Vector3(o, 0.01f, extent), c);
                    break;
                case ConceptViewMode.Profile:
                    World.DrawLine(origin + new Vector3(-extent, o, 0.01f), origin + new Vector3(extent, o, 0.01f), c);
                    World.DrawLine(origin + new Vector3(o, -extent, 0.01f), origin + new Vector3(o, extent, 0.01f), c);
                    break;
                case ConceptViewMode.Bow:
                    World.DrawLine(origin + new Vector3(-extent, o, 0.01f), origin + new Vector3(extent, o, 0.01f), c);
                    World.DrawLine(origin + new Vector3(o, -extent, 0.01f), origin + new Vector3(o, extent, 0.01f), c);
                    break;
            }
        }
    }

    private static Camera BuildOrbitCamera(SilkOrbitCamera orbit, int screenWidth, int screenHeight)
    {
        _ = screenWidth;
        _ = screenHeight;
        var eye = orbit.BuildEyePosition();
        return Camera.Perspective(eye, orbit.Target, Vector3.UnitY, orbit.FieldOfViewDegrees);
    }

    private static Camera BuildOrthographicCamera(ConceptViewMode mode, Vector3 center, float radius, int screenWidth, int screenHeight)
    {
        var aspect = screenWidth / (float)Math.Max(1, screenHeight);
        var halfHeight = radius * 1.15f;
        var halfWidth = halfHeight * aspect;
        var distance = radius * 4f;
        var fovy = halfHeight * 2f;

        Vector3 eye;
        Vector3 target = center;
        Vector3 up;
        switch (mode)
        {
            case ConceptViewMode.Plan:
                eye = center + new Vector3(0f, distance, 0f);
                up = Vector3.UnitZ;
                break;
            case ConceptViewMode.Profile:
                eye = center + new Vector3(distance, 0f, 0f);
                up = Vector3.UnitY;
                break;
            default:
                eye = center + new Vector3(0f, 0f, distance);
                up = Vector3.UnitY;
                break;
        }

        _ = halfWidth;
        return new Camera
        {
            Position = eye,
            Target = target,
            Up = up,
            Fovy = fovy,
            Projection = CameraProjection.Orthographic,
        };
    }

    private static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;

    private static Color ToColor(float[] rgb)
    {
        var r = rgb.Length > 0 ? (byte)(rgb[0] * 255) : (byte)200;
        var g = rgb.Length > 1 ? (byte)(rgb[1] * 255) : (byte)200;
        var b = rgb.Length > 2 ? (byte)(rgb[2] * 255) : (byte)200;
        return Color.FromArgb(255, r, g, b);
    }
}
