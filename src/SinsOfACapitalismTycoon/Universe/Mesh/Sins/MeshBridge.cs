using System.Collections.Immutable;

using Novolis.Economy.Logistics;
using Novolis.Simulation.Mesh;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Maps Astro campaign systems into <see cref="MeshState"/> (product glue).</summary>
internal static class MeshBridge
{
  internal static MeshState FromBridge(
    AstroEconomyBridge.BridgeResult bridge,
    MeshPolicy? policy = null,
    int? bandwidthOverride = null)
  {
    var p = policy ?? new MeshPolicy();
    var bw = bandwidthOverride ?? p.DefaultPulseBandwidthPerHour;

    var nodes = ImmutableDictionary.CreateBuilder<string, MeshNode>(StringComparer.OrdinalIgnoreCase);
    foreach (var h in bridge.Hubs)
    {
      var id = MeshNodeId.From(h.SystemId);
      nodes[id.Value] = new MeshNode(id, h.SystemId, h.Name, bw);
    }

    var edges = ImmutableArray.CreateBuilder<MeshEdge>();
    var seen = new HashSet<string>(StringComparer.Ordinal);

    foreach (var (fromId, adj) in bridge.Graph.Adjacency)
    {
      if (!nodes.ContainsKey(fromId))
      {
        continue;
      }

      foreach (var edge in adj)
      {
        if (!nodes.ContainsKey(edge.To.Value))
        {
          continue;
        }

        var key = $"{fromId}>{edge.To.Value}";
        if (!seen.Add(key))
        {
          continue;
        }

        edges.Add(ToMeshEdge(
          MeshNodeId.From(fromId),
          MeshNodeId.From(edge.To.Value),
          edge.DistanceLy,
          p));
      }
    }

    return MeshState.Empty(p) with
    {
      Nodes = nodes.ToImmutable(),
      Edges = edges.ToImmutable(),
    };
  }

  public static MeshState RegisterMailbox(
    MeshState state,
    MeshIdentityId id,
    MeshNodeId location,
    MeshIdentityKind? kind = null) =>
    MailboxEngine.Register(state, id, location, kind);

  public static int PulseTravelHours(double distanceLy, MeshPolicy policy) =>
    Math.Max(1, (int)Math.Ceiling(distanceLy / policy.PulseLyPerHour));

  public static int BulkTravelHours(double distanceLy, MeshPolicy policy) =>
    Math.Max(1, (int)Math.Ceiling(distanceLy / policy.BulkLyPerHour));

  private static MeshEdge ToMeshEdge(
    MeshNodeId from,
    MeshNodeId to,
    double ly,
    MeshPolicy policy) =>
    new(
      from,
      to,
      PulseTravelHours(ly, policy),
      BulkTravelHours(ly, policy),
      ly);
}
