using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Maps Astro campaign hubs into <see cref="MeshState"/> (product glue).</summary>
public static class MeshBridge
{
  internal static MeshState FromBridge(
    AstroEconomyBridge.BridgeResult bridge,
    MeshPolicy? policy = null,
    int? bandwidthOverride = null)
  {
    var p = policy ?? new MeshPolicy();
    var bw = bandwidthOverride ?? p.DefaultPulseBandwidthPerHour;

    var hubs = ImmutableDictionary.CreateBuilder<string, MeshHub>(StringComparer.OrdinalIgnoreCase);
    foreach (var h in bridge.Hubs)
    {
      var id = MeshHubId.From(h.SystemId);
      hubs[id.Value] = new MeshHub(id, h.SystemId, h.Name, bw);
    }

    var edges = ImmutableArray.CreateBuilder<MeshEdge>();
    var seen = new HashSet<string>(StringComparer.Ordinal);

    foreach (var (fromId, adj) in bridge.Graph.Adjacency)
    {
      if (!hubs.ContainsKey(fromId))
      {
        continue;
      }

      foreach (var edge in adj)
      {
        if (!hubs.ContainsKey(edge.To.Value))
        {
          continue;
        }

        var key = $"{fromId}>{edge.To.Value}";
        if (!seen.Add(key))
        {
          continue;
        }

        edges.Add(ToMeshEdge(
          MeshHubId.From(fromId),
          MeshHubId.From(edge.To.Value),
          edge.DistanceLy,
          p));
      }
    }

    return MeshState.Empty(p) with
    {
      Hubs = hubs.ToImmutable(),
      Edges = edges.ToImmutable(),
    };
  }

  public static MeshState RegisterIdentity(
    MeshState state,
    MeshIdentityId id,
    MeshHubId? lastKnownHub = null) =>
    state with
    {
      Identities = state.Identities.SetItem(
        id.Value,
        new MeshIdentityBinding(id, lastKnownHub)),
    };

  public static int PulseTravelHours(double distanceLy, MeshPolicy policy) =>
    Math.Max(1, (int)Math.Ceiling(distanceLy / policy.PulseLyPerHour));

  public static int BulkTravelHours(double distanceLy, MeshPolicy policy) =>
    Math.Max(1, (int)Math.Ceiling(distanceLy / policy.BulkLyPerHour));

  private static MeshEdge ToMeshEdge(
    MeshHubId from,
    MeshHubId to,
    double ly,
    MeshPolicy policy) =>
    new(
      from,
      to,
      PulseTravelHours(ly, policy),
      BulkTravelHours(ly, policy),
      ly);
}
