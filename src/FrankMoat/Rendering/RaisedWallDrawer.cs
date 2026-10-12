using System.Numerics;
using FrankMoat.Levels;
using Novolis.Math.Geometry;
using Novolis.Math.Topology;
using Novolis.Rendering.Planar;

namespace FrankMoat.Rendering;

internal sealed class RaisedWallDrawer
{
    private readonly List<PlanarWallFace> _faces = [];
    private readonly List<(PlanarStaticPolygon Polygon, Vector3 Center)> _parts = [];

    public void Build(
        PlanarScene scene,
        IReadOnlyList<RaisedWall> walls,
        FrankMoatArt art)
    {
        _faces.Clear();
        _parts.Clear();
        foreach (var wall in walls)
        {
            var firstPart = scene.StaticPolygons.Count;
            var prism = wall.Footprint.Extrude(PlanarElevation.Offset(wall.Height));
            var faces = PlanarWallComposer.Add(
                scene,
                prism,
                art.Wall(wall.Material),
                textureMeters: 1.6f,
                frontFacesOnly: true,
                capTexture: art.Wall(wall.Material),
                faceFill: art.WallSideTint(wall.Material),
                capFill: art.WallCapTint(wall.Material));
            _faces.AddRange(faces);
            var center = wall.Segments.Count == 0
                ? Vector3.Zero
                : (wall.Segments[0].Start + wall.Segments[0].End) * 0.5f;
            for (var i = firstPart; i < scene.StaticPolygons.Count; i++)
            {
                var polygon = scene.StaticPolygons[i];
                polygon.Layer = PlanarDrawLayer.World;
                polygon.SortKey = -(int)MathF.Round(MinimumZ(polygon.Shape) * 100f)
                    + (polygon.WallPart == PlanarWallPartKind.Cap ? -1 : 1);
                polygon.DrawOutline = true;
                polygon.OutlineColor = new Rgba32(12, 16, 22, 235);
                _parts.Add((polygon, center));
            }
        }
    }

    public void TickOcclusion(Vector3 frank, float muzzle)
    {
        PlanarWallComposer.TickOcclusion(_faces, frank, muzzle > 0f ? 1.14f : 1f);
        foreach (var part in _parts)
        {
            var distance = Vector3PlanarExtensions.DistanceXz(frank, part.Center);
            var readableAlpha = distance < 8f
                ? 132
                : distance < 14f
                    ? 182
                    : 224;
            var fill = part.Polygon.FillColor;
            part.Polygon.FillColor = new Rgba32(
                fill.R,
                fill.G,
                fill.B,
                (byte)Math.Min(fill.A, readableAlpha));
        }
    }

    private static float MinimumZ(Novolis.Math.Topology.Polygon shape)
    {
        var minimum = float.MaxValue;
        foreach (var vertex in shape)
        {
            minimum = MathF.Min(minimum, vertex.Z);
        }

        return minimum == float.MaxValue ? 0f : minimum;
    }
}
