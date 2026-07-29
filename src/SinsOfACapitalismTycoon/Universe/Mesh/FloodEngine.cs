using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>Fan-out identity/feed packets from nodes that hold them but have not yet seeded neighbors.</summary>
internal static class FloodEngine
{
  public static MeshState Dispatch(MeshState state)
  {
    foreach (var packet in state.Packets.Values)
    {
      if (packet.Destination.Kind is not (MeshAddressKind.Identity or MeshAddressKind.Feed))
      {
        continue;
      }

      var pk = MeshState.PacketKey(packet.Id);
      var seeded = state.FloodSeededAt.TryGetValue(pk, out var set)
        ? set
        : ImmutableHashSet<string>.Empty;

      var nodesHolding = state.NodeCaches
        .Where(kv => kv.Value.ContainsKey(pk))
        .Select(kv => kv.Key)
        .ToList();

      // Bias: if identity mailbox location is known, seed from there first when it holds the packet.
      if (packet.Destination.Kind == MeshAddressKind.Identity
          && packet.Destination.Identity is { } id
          && state.Mailboxes.TryGetValue(id.Value, out var box)
          && nodesHolding.Contains(box.LocationNodeId.Value))
      {
        nodesHolding.Remove(box.LocationNodeId.Value);
        nodesHolding.Insert(0, box.LocationNodeId.Value);
      }

      foreach (var nodeKey in nodesHolding)
      {
        if (seeded.Contains(nodeKey))
        {
          continue;
        }

        var from = MeshNodeId.From(nodeKey);
        foreach (var edge in state.Edges)
        {
          if (!edge.From.Equals(from))
          {
            continue;
          }

          if (state.IsVisibleAt(packet.Id, edge.To))
          {
            continue;
          }

          state = MeshVisibility.EnqueueLaunch(state, new PendingLaunch(
            packet.Id,
            from,
            edge.To,
            ImmutableArray<MeshNodeId>.Empty,
            IsFloodHop: true,
            packet.Priority));
        }

        seeded = seeded.Add(nodeKey);
        state = state with
        {
          FloodSeededAt = state.FloodSeededAt.SetItem(pk, seeded),
        };
      }
    }

    return state;
  }
}
