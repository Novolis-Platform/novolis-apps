using Novolis.Avalonia.StarMap;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>System-id path edges for the captain star map highlight.</summary>
internal static class RouteHighlight
{
  public static IReadOnlyList<StarMapEdge> BetweenSystems(
    CampaignWorld.Ids ids,
    string? fromSystemId,
    string? toSystemId)
  {
    if (string.IsNullOrWhiteSpace(fromSystemId)
        || string.IsNullOrWhiteSpace(toSystemId)
        || fromSystemId.Equals(toSystemId, StringComparison.OrdinalIgnoreCase))
    {
      return [];
    }

    var path = ShortestSystemPath(ids, fromSystemId, toSystemId);
    if (path.Count < 2)
    {
      return [];
    }

    var edges = new List<StarMapEdge>(path.Count - 1);
    for (var i = 0; i < path.Count - 1; i++)
    {
      edges.Add(new StarMapEdge { FromId = path[i], ToId = path[i + 1] });
    }

    return edges;
  }

  public static IReadOnlyList<StarMapEdge> FromShipment(
    CampaignWorld.Ids ids,
    EconomyWorld world,
    ActiveShipment shipment)
  {
    if (shipment.IsLegacy || shipment.Itinerary.CorridorIds.IsDefaultOrEmpty)
    {
      return [];
    }

    var edges = new List<StarMapEdge>();
    var start = Math.Max(0, shipment.LegIndex);
    for (var i = start; i < shipment.Itinerary.CorridorIds.Length; i++)
    {
      var cid = shipment.Itinerary.CorridorIds[i];
      if (!world.Corridors.TryGetValue(cid, out var corridor))
      {
        corridor = ids.Bridge.Corridors.FirstOrDefault(c => c.Id.Equals(cid));
        if (corridor is null)
        {
          continue;
        }
      }

      var fromSys = MeshMailboxSync.HubToSystemId(ids, corridor.From);
      var toSys = MeshMailboxSync.HubToSystemId(ids, corridor.To);
      if (fromSys is null || toSys is null)
      {
        continue;
      }

      edges.Add(new StarMapEdge { FromId = fromSys, ToId = toSys });
    }

    return edges;
  }

  public static IReadOnlyList<StarMapEdge> Merge(params IReadOnlyList<StarMapEdge>[] parts)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var list = new List<StarMapEdge>();
    foreach (var part in parts)
    {
      foreach (var e in part)
      {
        var a = string.Compare(e.FromId, e.ToId, StringComparison.OrdinalIgnoreCase) <= 0
          ? $"{e.FromId}|{e.ToId}"
          : $"{e.ToId}|{e.FromId}";
        if (seen.Add(a))
        {
          list.Add(e);
        }
      }
    }

    return list;
  }

  private static List<string> ShortestSystemPath(
    CampaignWorld.Ids ids,
    string fromSystemId,
    string toSystemId)
  {
    var adj = ids.Bridge.Graph.Adjacency;
    if (!adj.ContainsKey(fromSystemId) || !adj.ContainsKey(toSystemId))
    {
      return [];
    }

    var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var q = new Queue<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fromSystemId };
    q.Enqueue(fromSystemId);
    while (q.Count > 0)
    {
      var cur = q.Dequeue();
      if (cur.Equals(toSystemId, StringComparison.OrdinalIgnoreCase))
      {
        break;
      }

      if (!adj.TryGetValue(cur, out var edges))
      {
        continue;
      }

      foreach (var e in edges)
      {
        var next = e.To.Value;
        if (!seen.Add(next))
        {
          continue;
        }

        prev[next] = cur;
        q.Enqueue(next);
      }
    }

    if (!prev.ContainsKey(toSystemId)
        && !fromSystemId.Equals(toSystemId, StringComparison.OrdinalIgnoreCase))
    {
      return [];
    }

    var path = new List<string> { toSystemId };
    var walk = toSystemId;
    while (prev.TryGetValue(walk, out var p))
    {
      path.Add(p);
      walk = p;
    }

    path.Reverse();
    return path[0].Equals(fromSystemId, StringComparison.OrdinalIgnoreCase) ? path : [];
  }
}
