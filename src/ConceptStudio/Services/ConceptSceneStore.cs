using System.Numerics;
using ConceptStudio.Models;
using Novolis.Rendering.Compile;
using Novolis.Rendering.Materials;
using Novolis.Rendering.Runtime;
using Novolis.Rendering.Scene;

namespace ConceptStudio.Services;

internal sealed class ConceptSceneStore
{
    private readonly object _compileLock = new();
    private CompiledScene? _cachedScene;
    private int _cachedRevision = -1;

    public CompiledScene Compile(ConceptDocument document, int revision)
    {
        lock (_compileLock)
        {
            if (_cachedScene is not null && _cachedRevision == revision)
                return _cachedScene;

            _cachedScene = CompileCore(document);
            _cachedRevision = revision;
            return _cachedScene;
        }
    }

    public void InvalidateCompileCache()
    {
        lock (_compileLock)
        {
            _cachedRevision = -1;
            _cachedScene = null;
        }
    }

    private static CompiledScene CompileCore(ConceptDocument document)
    {
        var builder = new SceneBuilder()
            .AddGround(MaterialPresets.Standard(new Vector3(0.22f, 0.24f, 0.28f), roughness: 0.92f), size: 40f)
            .AddDirectionalLight(new Vector3(-0.4f, -1f, -0.25f), new Vector3(1f, 0.98f, 0.94f), 2.2f);

        foreach (var part in document.Parts)
            ConceptPrimitiveBuilder.AddPart(builder, part);

        return SceneCompiler.Compile(builder.Build());
    }
}

internal static class SceneBounds
{
    public static (Vector3 Center, float Radius) Compute(ConceptDocument document)
    {
        var parts = document.Parts.Where(p => !p.IsGroup).ToList();
        if (parts.Count == 0)
            return (new Vector3(0f, 1f, 0f), 12f);

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);

        foreach (var part in parts)
        {
            var center = ConceptMaterialMapper.ToVector3(part.Center);
            var scale = part.Scale.Length >= 3
                ? new Vector3(part.Scale[0], part.Scale[1], part.Scale[2])
                : Vector3.One;

            switch (part.Kind.ToLowerInvariant())
            {
                case "sphere":
                {
                    var r = part.Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
                    Expand(ref min, ref max, center - new Vector3(r));
                    Expand(ref min, ref max, center + new Vector3(r));
                    break;
                }
                case "cylinder":
                case "cone":
                {
                    var r = part.Radius * MathF.Max(scale.X, scale.Z);
                    var h = part.Height * scale.Y * 0.5f;
                    Expand(ref min, ref max, center - new Vector3(r, h, r));
                    Expand(ref min, ref max, center + new Vector3(r, h, r));
                    break;
                }
                default:
                {
                    var he = part.HalfExtents.Length >= 3
                        ? new Vector3(part.HalfExtents[0] * scale.X, part.HalfExtents[1] * scale.Y, part.HalfExtents[2] * scale.Z)
                        : new Vector3(0.5f);
                    Expand(ref min, ref max, center - he);
                    Expand(ref min, ref max, center + he);
                    break;
                }
            }
        }

        var boundsCenter = (min + max) * 0.5f;
        var extent = max - min;
        var radius = MathF.Max(2f, MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z)) * 0.65f);
        return (boundsCenter, radius);
    }

    private static void Expand(ref Vector3 min, ref Vector3 max, Vector3 point)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }
}

internal static class GridSnap
{
    public static float Snap(float value, float step, bool enabled) =>
        enabled ? MathF.Round(value / step) * step : value;

    public static void SnapCenter(float[] center, float step, bool enabled)
    {
        if (!enabled || center.Length < 3)
            return;

        center[0] = Snap(center[0], step, true);
        center[1] = Snap(center[1], step, true);
        center[2] = Snap(center[2], step, true);
    }
}
