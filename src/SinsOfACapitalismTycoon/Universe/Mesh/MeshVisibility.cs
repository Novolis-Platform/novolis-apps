using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>Credit node caches; enqueue launches; enforce local cache caps by priority.</summary>
internal static class MeshVisibility
{
  public static MeshState CreditNode(MeshState state, PacketId packet, MeshNodeId node)
  {
    var key = MeshState.PacketKey(packet);
    var nodeKey = node.Value;
    var existing = state.NodeCaches.TryGetValue(nodeKey, out var map)
      ? map
      : ImmutableDictionary<string, NodeCacheEntry>.Empty;
    if (existing.ContainsKey(key))
    {
      return state;
    }

    if (!state.TryGetPacket(packet, out var pkt))
    {
      return state;
    }

    existing = existing.SetItem(key, new NodeCacheEntry(state.HourIndex, pkt.LocalRetentionPriority, pkt.LocalTtlHours));
    state = state with
    {
      NodeCaches = state.NodeCaches.SetItem(nodeKey, existing),
      Stats = state.Stats with { CacheCredits = state.Stats.CacheCredits + 1 },
    };
    state = EnforceNodeCacheCap(state, node);
    state = ApplyRetractionIfNeeded(state, pkt, node);
    state = MailboxEngine.PushAtNode(state, packet, node);
    return FeedEngine.ForceMandatoryAtNode(state, packet, node);
  }

  static MeshState ApplyRetractionIfNeeded(MeshState state, MeshPacket pkt, MeshNodeId node)
  {
    if (!pkt.Topic.Equals(MeshTopics.SpotRetract, StringComparison.Ordinal)
        || string.IsNullOrEmpty(pkt.LogicalKey))
    {
      return state;
    }

    var nodeKey = node.Value;
    var map = state.NodeRetractions.TryGetValue(nodeKey, out var existing)
      ? existing
      : ImmutableDictionary<string, NodeCacheEntry>.Empty;
    if (map.ContainsKey(pkt.LogicalKey))
    {
      return state;
    }

    map = map.SetItem(pkt.LogicalKey, new NodeCacheEntry(state.HourIndex, pkt.LocalRetentionPriority, pkt.LocalTtlHours));
    return state with
    {
      NodeRetractions = state.NodeRetractions.SetItem(nodeKey, map),
      Stats = state.Stats with { RetractionsApplied = state.Stats.RetractionsApplied + 1 },
    };
  }

  /// <summary>Drop lowest local-priority (then oldest) entries when over <see cref="MeshPolicy.MaxPacketsPerNodeCache"/>.</summary>
  public static MeshState EnforceNodeCacheCap(MeshState state, MeshNodeId node)
  {
    var cap = state.Policy.MaxPacketsPerNodeCache;
    if (cap <= 0
        || !state.NodeCaches.TryGetValue(node.Value, out var map)
        || map.Count <= cap)
    {
      return state;
    }

    var victims = map
      .OrderBy(kv => kv.Value.LocalPriority)
      .ThenBy(kv => kv.Value.ReceivedHour)
      .Take(map.Count - cap)
      .Select(kv => kv.Key)
      .ToList();

    foreach (var pk in victims)
    {
      state = TtlEngine.DropLocal(state, MeshNodeId.From(node.Value), pk, reopenFlood: true);
    }

    return state;
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
