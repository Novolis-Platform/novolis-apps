using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Shortest path on pulse travel hours (Dijkstra).</summary>
public static class MeshPathfinder
{
  public static ImmutableArray<MeshHubId>? FindPath(
    MeshState state,
    MeshHubId origin,
    MeshHubId destination)
  {
    if (origin.Equals(destination))
    {
      return ImmutableArray.Create(origin);
    }

    var adjacency = BuildAdjacency(state);
    if (!adjacency.ContainsKey(origin.Value) || !state.Hubs.ContainsKey(destination.Value))
    {
      return null;
    }

    var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [origin.Value] = 0 };
    var prev = new Dictionary<string, string?>(StringComparer.Ordinal);
    var pq = new PriorityQueue<string, int>();
    pq.Enqueue(origin.Value, 0);

    while (pq.Count > 0)
    {
      var u = pq.Dequeue();
      if (u == destination.Value)
      {
        break;
      }

      if (!adjacency.TryGetValue(u, out var edges))
      {
        continue;
      }

      var du = dist[u];
      foreach (var (to, hours) in edges)
      {
        var alt = du + hours;
        if (!dist.TryGetValue(to, out var known) || alt < known)
        {
          dist[to] = alt;
          prev[to] = u;
          pq.Enqueue(to, alt);
        }
      }
    }

    if (!dist.ContainsKey(destination.Value))
    {
      return null;
    }

    var stack = new Stack<MeshHubId>();
    string? cur = destination.Value;
    while (cur is not null)
    {
      stack.Push(MeshHubId.From(cur));
      prev.TryGetValue(cur, out cur);
    }

    return stack.ToImmutableArray();
  }

  public static int TravelHours(MeshState state, MeshHubId from, MeshHubId to, MeshTrafficLayer layer)
  {
    foreach (var e in state.Edges)
    {
      if (e.From.Equals(from) && e.To.Equals(to))
      {
        return layer == MeshTrafficLayer.Bulk ? e.BulkTravelHours : e.PulseTravelHours;
      }
    }

    return int.MaxValue;
  }

  private static Dictionary<string, List<(string To, int Hours)>> BuildAdjacency(MeshState state)
  {
    var map = new Dictionary<string, List<(string, int)>>(StringComparer.Ordinal);
    foreach (var e in state.Edges)
    {
      if (!map.TryGetValue(e.From.Value, out var list))
      {
        list = [];
        map[e.From.Value] = list;
      }

      list.Add((e.To.Value, e.PulseTravelHours));
    }

    return map;
  }
}
