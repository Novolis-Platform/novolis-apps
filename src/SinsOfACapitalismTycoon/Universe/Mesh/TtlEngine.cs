using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>
/// Dual TTL: <see cref="MeshPacket.LocalTtlHours"/> drops a node cache after receive;
/// <see cref="MeshPacket.GlobalTtlHours"/> is the earliest universal removal of the packet.
/// Local retention priority governs capacity eviction (see <see cref="MeshVisibility.EnforceNodeCacheCap"/>).
/// </summary>
internal static class TtlEngine
{
  public static MeshState Expire(MeshState state)
  {
    state = ExpireLocal(state);
    state = ExpireGlobal(state);
    return state;
  }

  /// <summary>Remove packet from one node's cache; optionally reopen flood so neighbors can refill.</summary>
  public static MeshState DropLocal(
    MeshState state,
    MeshNodeId node,
    string packetKey,
    bool reopenFlood)
  {
    if (!state.NodeCaches.TryGetValue(node.Value, out var map) || !map.ContainsKey(packetKey))
    {
      return state;
    }

    map = map.Remove(packetKey);
    state = state with
    {
      NodeCaches = map.Count == 0
        ? state.NodeCaches.Remove(node.Value)
        : state.NodeCaches.SetItem(node.Value, map),
      Stats = state.Stats with { LocalCacheDrops = state.Stats.LocalCacheDrops + 1 },
    };

    if (!reopenFlood || !state.FloodSeededAt.TryGetValue(packetKey, out var seeded))
    {
      return state;
    }

    // Neighbors that already seeded will not re-offer; clear their seeded bit if they edge to this node.
    var nextSeeded = seeded;
    foreach (var edge in state.Edges)
    {
      if (!edge.To.Equals(node) || !seeded.Contains(edge.From.Value))
      {
        continue;
      }

      nextSeeded = nextSeeded.Remove(edge.From.Value);
    }

    nextSeeded = nextSeeded.Remove(node.Value);
    return state with
    {
      FloodSeededAt = nextSeeded.Count == 0
        ? state.FloodSeededAt.Remove(packetKey)
        : state.FloodSeededAt.SetItem(packetKey, nextSeeded),
    };
  }

  private static MeshState ExpireLocal(MeshState state)
  {
    var drops = new List<(string Node, string PacketKey)>();
    foreach (var (nodeKey, map) in state.NodeCaches)
    {
      foreach (var (pk, entry) in map)
      {
        var localTtl = entry.LocalTtlHours
                       ?? (state.Packets.TryGetValue(pk, out var packet) ? packet.LocalTtlHours : null);
        if (localTtl is not { } ttl)
        {
          continue;
        }

        if (state.HourIndex - entry.ReceivedHour >= ttl)
        {
          drops.Add((nodeKey, pk));
        }
      }
    }

    foreach (var (node, pk) in drops)
    {
      state = DropLocal(state, MeshNodeId.From(node), pk, reopenFlood: true);
    }

    // Retraction marks expire on their own local TTL (sticky by default).
    var retractDrops = new List<(string Node, string LogicalKey)>();
    foreach (var (nodeKey, map) in state.NodeRetractions)
    {
      foreach (var (logicalKey, entry) in map)
      {
        if (entry.LocalTtlHours is not { } ttl)
        {
          continue;
        }

        if (state.HourIndex - entry.ReceivedHour >= ttl)
        {
          retractDrops.Add((nodeKey, logicalKey));
        }
      }
    }

    foreach (var (node, logicalKey) in retractDrops)
    {
      if (!state.NodeRetractions.TryGetValue(node, out var map))
      {
        continue;
      }

      map = map.Remove(logicalKey);
      state = state with
      {
        NodeRetractions = map.Count == 0
          ? state.NodeRetractions.Remove(node)
          : state.NodeRetractions.SetItem(node, map),
      };
    }

    return state;
  }

  private static MeshState ExpireGlobal(MeshState state)
  {
    var dead = new HashSet<string>(StringComparer.Ordinal);
    foreach (var packet in state.Packets.Values)
    {
      if (packet.GlobalTtlHours is not { } globalTtl)
      {
        continue;
      }

      // Earliest universal removal — not before PublishedHour + GlobalTtlHours.
      if (state.HourIndex - packet.PublishedHour >= globalTtl)
      {
        dead.Add(MeshState.PacketKey(packet.Id));
      }
    }

    if (dead.Count == 0)
    {
      return state;
    }

    var packets = state.Packets;
    foreach (var k in dead)
    {
      packets = packets.Remove(k);
    }

    var caches = state.NodeCaches;
    foreach (var kv in state.NodeCaches)
    {
      var next = kv.Value;
      foreach (var k in dead)
      {
        next = next.Remove(k);
      }

      caches = next.Count == 0 ? caches.Remove(kv.Key) : caches.SetItem(kv.Key, next);
    }

    var mailboxes = state.Mailboxes;
    foreach (var kv in state.Mailboxes)
    {
      mailboxes = mailboxes.SetItem(
        kv.Key,
        kv.Value with
        {
          PushedPacketKeys = kv.Value.PushedPacketKeys.Except(dead).ToImmutableHashSet(StringComparer.Ordinal),
        });
    }

    var inboxes = state.FeedInboxes;
    foreach (var kv in state.FeedInboxes)
    {
      var next = kv.Value.Except(dead).ToImmutableHashSet(StringComparer.Ordinal);
      inboxes = next.Count == 0 ? inboxes.Remove(kv.Key) : inboxes.SetItem(kv.Key, next);
    }

    var pending = state.Pending
      .Where(p => !dead.Contains(MeshState.PacketKey(p.PacketId)))
      .ToImmutableArray();

    var drones = state.Drones
      .Where(d => !dead.Contains(MeshState.PacketKey(d.PacketId)))
      .ToImmutableArray();

    var flood = state.FloodSeededAt;
    foreach (var k in dead)
    {
      flood = flood.Remove(k);
    }

    return state with
    {
      Packets = packets,
      NodeCaches = caches,
      Mailboxes = mailboxes,
      FeedInboxes = inboxes,
      Pending = pending,
      Drones = drones,
      FloodSeededAt = flood,
      Stats = state.Stats with { GlobalPacketDrops = state.Stats.GlobalPacketDrops + dead.Count },
    };
  }
}
