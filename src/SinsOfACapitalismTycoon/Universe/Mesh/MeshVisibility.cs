using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Credit node caches; enqueue launches.</summary>
public static class MeshVisibility
{
  public static MeshState CreditNode(MeshState state, PacketId packet, MeshNodeId node)
  {
    var key = MeshState.PacketKey(packet);
    var nodeKey = node.Value;
    var existing = state.NodeCaches.TryGetValue(nodeKey, out var set)
      ? set
      : ImmutableHashSet<string>.Empty;
    if (existing.Contains(key))
    {
      return state;
    }

    state = state with
    {
      NodeCaches = state.NodeCaches.SetItem(nodeKey, existing.Add(key)),
      Stats = state.Stats with { CacheCredits = state.Stats.CacheCredits + 1 },
    };
    state = MailboxEngine.PushAtNode(state, packet, node);
    return FeedEngine.ForceMandatoryAtNode(state, packet, node);
  }

  public static MeshState EnqueueLaunch(MeshState state, PendingLaunch launch)
  {
    foreach (var p in state.Pending)
    {
      if (p.PacketId.Equals(launch.PacketId)
          && p.From.Equals(launch.From)
          && p.To.Equals(launch.To)
          && p.IsFloodHop == launch.IsFloodHop)
      {
        return state;
      }
    }

    if (launch.IsFloodHop && state.IsVisibleAt(launch.PacketId, launch.To))
    {
      return state;
    }

    foreach (var d in state.Drones)
    {
      if (d.PacketId.Equals(launch.PacketId) && d.From.Equals(launch.From) && d.To.Equals(launch.To))
      {
        return state;
      }
    }

    var pendingAtNode = state.Pending.Count(p => p.From.Equals(launch.From));
    if (pendingAtNode >= state.Policy.MaxPendingPerHub)
    {
      return state with
      {
        Stats = state.Stats with { BandwidthDeferred = state.Stats.BandwidthDeferred + 1 },
      };
    }

    return state with { Pending = state.Pending.Add(launch) };
  }
}
