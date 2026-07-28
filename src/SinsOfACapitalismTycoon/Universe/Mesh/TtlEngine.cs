using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Drop TTL-expired packets from caches, mailboxes, and pending (drones may finish).</summary>
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

    var caches = state.HubCaches;
    foreach (var kv in state.HubCaches)
    {
      var next = kv.Value.Except(dead).ToImmutableHashSet(StringComparer.Ordinal);
      caches = next.Count == 0 ? caches.Remove(kv.Key) : caches.SetItem(kv.Key, next);
    }

    var mailboxes = state.Mailboxes;
    foreach (var kv in state.Mailboxes)
    {
      var next = kv.Value.Except(dead).ToImmutableHashSet(StringComparer.Ordinal);
      mailboxes = next.Count == 0
        ? mailboxes.Remove(kv.Key)
        : mailboxes.SetItem(kv.Key, next);
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
      HubCaches = caches,
      Mailboxes = mailboxes,
      Pending = pending,
      Drones = drones,
      FloodSeededAt = flood,
    };
  }
}
