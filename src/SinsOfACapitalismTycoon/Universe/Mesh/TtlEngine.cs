using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Drop TTL-expired packets from caches, mailboxes, feed inboxes, and pending.</summary>
public static class TtlEngine
{
  public static MeshState Expire(MeshState state)
  {
    var dead = new HashSet<string>(StringComparer.Ordinal);
    foreach (var packet in state.Packets.Values)
    {
      if (packet.TtlHours is not { } ttl)
      {
        continue;
      }

      if (state.HourIndex - packet.PublishedHour >= ttl)
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
      var next = kv.Value.Except(dead).ToImmutableHashSet(StringComparer.Ordinal);
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
    };
  }
}
