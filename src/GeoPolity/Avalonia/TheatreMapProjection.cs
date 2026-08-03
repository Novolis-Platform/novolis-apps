using Novolis.Avalonia.StarMap;
using Novolis.Geopolitics.Core;

namespace GeoPolity.AvaloniaUi;

/// <summary>World → StarMap systems (polities) and border edges.</summary>
public static class TheatreMapProjection
{
    public static (IReadOnlyList<StarMapPoint> Points, IReadOnlyList<StarMapEdge> Edges) Project(WorldState world)
    {
        var points = world.Polities
            .Select(p => new StarMapPoint
            {
                Id = p.Id.Value.ToString(),
                Label = p.Name,
                X = p.MapX,
                Y = p.MapY,
            })
            .ToList();

        var edges = new List<StarMapEdge>();
        var seen = new HashSet<long>();
        foreach (var pr in world.Provinces)
        {
            foreach (var n in pr.Neighbors)
            {
                var other = world.Province(n);
                if (other.OwnerId == pr.OwnerId)
                {
                    continue;
                }

                var a = pr.OwnerId.Value;
                var b = other.OwnerId.Value;
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!seen.Add(key))
                {
                    continue;
                }

                edges.Add(new StarMapEdge
                {
                    FromId = a.ToString(),
                    ToId = b.ToString(),
                    BandTag = world.Polity(pr.OwnerId).Continent == world.Polity(other.OwnerId).Continent
                        ? "cluster"
                        : "bridge",
                });
            }
        }

        return (points, edges);
    }
}
