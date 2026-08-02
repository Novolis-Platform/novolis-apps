using Novolis.Astro.Abstractions;
using Novolis.Avalonia.StarMap;
using SinsOfACapitalismTycoon.Universe;

namespace SinsOfACapitalismTycoon.Ui;

/// <summary>Map projection helpers for <see cref="CaptainBridgeModel"/> — keeps UI composition thin.</summary>
internal static class CaptainMapProjection
{
  public static (IReadOnlyList<StarMapPoint> Points, IReadOnlyList<StarMapEdge> Edges,
    IReadOnlyDictionary<string, CampaignBriefingModel.HubDetail> HubDetails)
    Build(CampaignWorld.Ids ids)
  {
    var catalog = SinsCatalog.Load();
    var hubDetails = new Dictionary<string, CampaignBriefingModel.HubDetail>(StringComparer.OrdinalIgnoreCase);
    var points = new List<StarMapPoint>(ids.Bridge.Hubs.Count);
    foreach (var hub in ids.Bridge.Hubs)
    {
      hubDetails[hub.SystemId] = new CampaignBriefingModel.HubDetail(
        hub.SystemId,
        hub.Name,
        hub.Role.ToString(),
        $"Ag {hub.Profile.Potential.Agriculture:0.##} · Ind {hub.Profile.Potential.Industry:0.##} · Mine {hub.Profile.Potential.Mining:0.##}");

      if (!catalog.TryGet(new SystemId(hub.SystemId), out var star) || star is null)
      {
        continue;
      }

      points.Add(new StarMapPoint
      {
        Id = hub.SystemId,
        Label = $"{hub.Name} ({hub.Role})",
        X = star.Coords.X,
        Y = star.Coords.Y,
      });
    }

    var edges = new List<StarMapEdge>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var (fromId, list) in ids.Bridge.Graph.Adjacency)
    {
      foreach (var edge in list)
      {
        var a = fromId;
        var b = edge.To.Value;
        var key = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        if (!seen.Add(key))
        {
          continue;
        }

        edges.Add(new StarMapEdge
        {
          FromId = a,
          ToId = b,
          BandTag = edge.BandTag,
        });
      }
    }

    return (points, edges, hubDetails);
  }
}
