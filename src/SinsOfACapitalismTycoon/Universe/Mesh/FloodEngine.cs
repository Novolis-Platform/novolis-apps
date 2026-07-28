using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Fan-out flood/public packets from hubs that hold them but have not yet seeded neighbors.</summary>
public static class FloodEngine
{
  public static MeshState Dispatch(MeshState state)
  {
    foreach (var packet in state.Packets.Values)
    {
      if (packet.Destination.Kind is not (MeshAddressKind.Identity or MeshAddressKind.Public))
      {
        continue;
      }

      var pk = MeshState.PacketKey(packet.Id);
      var seeded = state.FloodSeededAt.TryGetValue(pk, out var set)
        ? set
        : ImmutableHashSet<string>.Empty;

      // Prefer last-known hub bias: process that hub first by ordering
      var hubsHolding = state.HubCaches
        .Where(kv => kv.Value.Contains(pk))
        .Select(kv => kv.Key)
        .ToList();

      if (packet.Destination.Kind == MeshAddressKind.Identity
          && packet.Destination.Identity is { } id
          && state.Identities.TryGetValue(id.Value, out var binding)
          && binding.LastKnownHub is { } last
          && hubsHolding.Contains(last.Value))
      {
        hubsHolding.Remove(last.Value);
        hubsHolding.Insert(0, last.Value);
      }

      foreach (var hubKey in hubsHolding)
      {
        if (seeded.Contains(hubKey))
        {
          continue;
        }

        var from = MeshHubId.From(hubKey);
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
            ImmutableArray<MeshHubId>.Empty,
            IsFloodHop: true,
            packet.Priority));
        }

        seeded = seeded.Add(hubKey);
        state = state with
        {
          FloodSeededAt = state.FloodSeededAt.SetItem(pk, seeded),
        };
      }
    }

    return state;
  }
}
