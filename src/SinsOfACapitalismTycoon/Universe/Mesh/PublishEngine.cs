using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Publish into the mesh — visibility / push / feed contract, never human delivery.</summary>
public static class PublishEngine
{
  public static MeshState Publish(
    MeshState state,
    MeshPacket packet,
    MeshNodeId fromNode)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(packet);
    if (!state.Nodes.ContainsKey(fromNode.Value))
    {
      throw new InvalidOperationException($"Unknown origin node {fromNode}.");
    }

    if (!packet.OriginNode.Equals(fromNode))
    {
      packet = packet with { OriginNode = fromNode };
    }

    if (packet.PublishedHour == 0 && state.HourIndex > 0)
    {
      packet = packet with { PublishedHour = state.HourIndex };
    }

    if (packet.Destination.Kind == MeshAddressKind.Feed && packet.Destination.Feed is null)
    {
      throw new InvalidOperationException("Feed address requires a Feed id.");
    }

    var key = MeshState.PacketKey(packet.Id);
    if (state.Packets.ContainsKey(key))
    {
      throw new InvalidOperationException($"Packet {packet.Id.Value} already published.");
    }

    state = state with { Packets = state.Packets.SetItem(key, packet) };
    // Credits node cache + pushes identity mail only if a mailbox is co-located here.
    state = MeshVisibility.CreditNode(state, packet.Id, fromNode);

    switch (packet.Destination.Kind)
    {
      case MeshAddressKind.Place:
        return PublishDirected(state, packet, fromNode);
      case MeshAddressKind.Identity:
        state = state with
        {
          Stats = state.Stats with { IdentityPublishes = state.Stats.IdentityPublishes + 1 },
        };
        return MarkFloodSeed(state, packet.Id, fromNode);
      case MeshAddressKind.Feed:
        state = state with
        {
          Stats = state.Stats with { FeedPublishes = state.Stats.FeedPublishes + 1 },
        };
        return MarkFloodSeed(state, packet.Id, fromNode);
      default:
        throw new InvalidOperationException($"Unknown address kind {packet.Destination.Kind}.");
    }
  }

  public static (MeshState State, PacketId Id) PublishPulse(
    MeshState state,
    MeshNodeId fromNode,
    MeshAddress destination,
    int priority = 1,
    bool sealedPacket = true,
    int? ttlHours = null,
    PacketId? id = null)
  {
    var packetId = id ?? PacketId.New();
    var layer = destination.Kind == MeshAddressKind.Feed
      ? MeshTrafficLayer.Feed
      : MeshTrafficLayer.Pulse;
    var packet = new MeshPacket(
      packetId,
      layer,
      sealedPacket,
      ImmutableArray<byte>.Empty,
      priority,
      ttlHours,
      fromNode,
      destination,
      state.HourIndex);
    return (Publish(state, packet, fromNode), packetId);
  }

  private static MeshState PublishDirected(MeshState state, MeshPacket packet, MeshNodeId fromNode)
  {
    var dest = packet.Destination.Place
      ?? throw new InvalidOperationException("Place address requires Place node.");
    state = state with
    {
      Stats = state.Stats with { DirectedPublishes = state.Stats.DirectedPublishes + 1 },
    };

    if (fromNode.Equals(dest))
    {
      return state;
    }

    var path = MeshPathfinder.FindPath(state, fromNode, dest);
    if (path is null || path.Value.Length < 2)
    {
      throw new InvalidOperationException($"No mesh path {fromNode} → {dest}.");
    }

    var hops = path.Value;
    var next = hops[1];
    var remaining = hops.Length > 2
      ? hops.Skip(2).ToImmutableArray()
      : ImmutableArray<MeshNodeId>.Empty;

    return MeshVisibility.EnqueueLaunch(state, new PendingLaunch(
      packet.Id,
      fromNode,
      next,
      remaining,
      IsFloodHop: false,
      packet.Priority));
  }

  internal static MeshState MarkFloodSeed(MeshState state, PacketId packet, MeshNodeId node)
  {
    var pk = MeshState.PacketKey(packet);
    var existing = state.FloodSeededAt.TryGetValue(pk, out var set)
      ? set
      : ImmutableHashSet<string>.Empty;
    return state with
    {
      FloodSeededAt = state.FloodSeededAt.SetItem(pk, existing),
    };
  }
}
