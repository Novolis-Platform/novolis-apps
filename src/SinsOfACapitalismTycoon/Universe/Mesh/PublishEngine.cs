using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Publish into the mesh — returns visibility contract, never delivery.</summary>
public static class PublishEngine
{
  public static MeshState Publish(
    MeshState state,
    MeshPacket packet,
    MeshHubId fromHub)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(packet);
    if (!state.Hubs.ContainsKey(fromHub.Value))
    {
      throw new InvalidOperationException($"Unknown origin hub {fromHub}.");
    }

    if (!packet.OriginHub.Equals(fromHub))
    {
      packet = packet with { OriginHub = fromHub };
    }

    if (packet.PublishedHour == 0 && state.HourIndex > 0)
    {
      packet = packet with { PublishedHour = state.HourIndex };
    }

    var key = MeshState.PacketKey(packet.Id);
    if (state.Packets.ContainsKey(key))
    {
      throw new InvalidOperationException($"Packet {packet.Id.Value} already published.");
    }

    state = state with { Packets = state.Packets.SetItem(key, packet) };
    state = MeshVisibility.CreditHub(state, packet.Id, fromHub);

    switch (packet.Destination.Kind)
    {
      case MeshAddressKind.Place:
        return PublishDirected(state, packet, fromHub);
      case MeshAddressKind.Identity:
        state = state with
        {
          Stats = state.Stats with { FloodPublishes = state.Stats.FloodPublishes + 1 },
        };
        if (packet.Destination.Identity is { } identity)
        {
          state = MeshVisibility.CreditMailbox(state, packet.Id, identity);
        }

        return MarkFloodSeed(state, packet.Id, fromHub);
      case MeshAddressKind.Public:
        state = state with
        {
          Stats = state.Stats with { PublicPublishes = state.Stats.PublicPublishes + 1 },
        };
        return MarkFloodSeed(state, packet.Id, fromHub);
      default:
        throw new InvalidOperationException($"Unknown address kind {packet.Destination.Kind}.");
    }
  }

  /// <summary>Convenience: build pulse packet and publish.</summary>
  public static (MeshState State, PacketId Id) PublishPulse(
    MeshState state,
    MeshHubId fromHub,
    MeshAddress destination,
    int priority = 1,
    bool sealedPacket = true,
    int? ttlHours = null,
    PacketId? id = null)
  {
    var packetId = id ?? PacketId.New();
    var packet = new MeshPacket(
      packetId,
      MeshTrafficLayer.Pulse,
      sealedPacket,
      ImmutableArray<byte>.Empty,
      priority,
      ttlHours,
      fromHub,
      destination,
      state.HourIndex);
    return (Publish(state, packet, fromHub), packetId);
  }

  private static MeshState PublishDirected(MeshState state, MeshPacket packet, MeshHubId fromHub)
  {
    var dest = packet.Destination.Place
      ?? throw new InvalidOperationException("Place address requires Place hub.");
    state = state with
    {
      Stats = state.Stats with { DirectedPublishes = state.Stats.DirectedPublishes + 1 },
    };

    if (fromHub.Equals(dest))
    {
      return state;
    }

    var path = MeshPathfinder.FindPath(state, fromHub, dest);
    if (path is null || path.Value.Length < 2)
    {
      throw new InvalidOperationException($"No mesh path {fromHub} → {dest}.");
    }

    var hops = path.Value;
    var next = hops[1];
    var remaining = hops.Length > 2
      ? hops.Skip(2).ToImmutableArray()
      : ImmutableArray<MeshHubId>.Empty;

    return MeshVisibility.EnqueueLaunch(state, new PendingLaunch(
      packet.Id,
      fromHub,
      next,
      remaining,
      IsFloodHop: false,
      packet.Priority));
  }

  internal static MeshState MarkFloodSeed(MeshState state, PacketId packet, MeshHubId hub)
  {
    var pk = MeshState.PacketKey(packet);
    var existing = state.FloodSeededAt.TryGetValue(pk, out var set)
      ? set
      : ImmutableHashSet<string>.Empty;
    if (existing.Contains(hub.Value))
    {
      return state;
    }

    // Clear hub from "already seeded" so FloodDispatch will fan out — use empty then add marker differently.
    // FloodDispatch looks for packets visible at hub that need fan-out; FloodSeededAt tracks completed fan-out.
    return state with
    {
      FloodSeededAt = state.FloodSeededAt.SetItem(pk, existing),
    };
  }
}
