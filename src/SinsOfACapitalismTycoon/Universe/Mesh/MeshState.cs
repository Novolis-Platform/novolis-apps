using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Immutable aggregate mesh state (stocks).</summary>
public sealed record MeshState(
  long HourIndex,
  MeshPolicy Policy,
  ImmutableDictionary<string, MeshNode> Nodes,
  ImmutableArray<MeshEdge> Edges,
  ImmutableDictionary<string, MeshPacket> Packets,
  ImmutableArray<InFlightDrone> Drones,
  ImmutableArray<PendingLaunch> Pending,
  /// <summary>NodeId.Value → packet keys visible at the node cache.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> NodeCaches,
  /// <summary>IdentityId.Value → mailbox (location + pushed private packets).</summary>
  ImmutableDictionary<string, MeshMailbox> Mailboxes,
  /// <summary>IdentityId.Value → subscribed feed ids.</summary>
  ImmutableDictionary<string, MeshSubscriptionBook> Subscriptions,
  /// <summary>IdentityId.Value → packet keys pulled from feeds at the local node.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> FeedInboxes,
  /// <summary>NodeId.Value → launches already consumed this hour.</summary>
  ImmutableDictionary<string, int> BandwidthUsedThisHour,
  /// <summary>PacketKey → nodes that already initiated flood fan-out.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> FloodSeededAt,
  /// <summary>PacketKey → loss count (for MaxLossesPerPacket).</summary>
  ImmutableDictionary<string, int> PacketLossCounts,
  MeshStats Stats)
{
  public static MeshState Empty(MeshPolicy? policy = null) => new(
    HourIndex: 0,
    Policy: policy ?? new MeshPolicy(),
    Nodes: ImmutableDictionary<string, MeshNode>.Empty,
    Edges: ImmutableArray<MeshEdge>.Empty,
    Packets: ImmutableDictionary<string, MeshPacket>.Empty,
    Drones: ImmutableArray<InFlightDrone>.Empty,
    Pending: ImmutableArray<PendingLaunch>.Empty,
    NodeCaches: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    Mailboxes: ImmutableDictionary<string, MeshMailbox>.Empty,
    Subscriptions: ImmutableDictionary<string, MeshSubscriptionBook>.Empty,
    FeedInboxes: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    BandwidthUsedThisHour: ImmutableDictionary<string, int>.Empty,
    FloodSeededAt: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    PacketLossCounts: ImmutableDictionary<string, int>.Empty,
    Stats: new MeshStats());

  public bool IsVisibleAt(PacketId packet, MeshNodeId node) =>
    NodeCaches.TryGetValue(node.Value, out var set) && set.Contains(PacketKey(packet));

  public bool IsInMailbox(PacketId packet, MeshIdentityId identity) =>
    Mailboxes.TryGetValue(identity.Value, out var box)
    && box.PushedPacketKeys.Contains(PacketKey(packet));

  public bool IsInFeedInbox(PacketId packet, MeshIdentityId identity) =>
    FeedInboxes.TryGetValue(identity.Value, out var set) && set.Contains(PacketKey(packet));

  public bool TryGetPacket(PacketId id, out MeshPacket packet)
  {
    if (Packets.TryGetValue(PacketKey(id), out packet!))
    {
      return true;
    }

    packet = null!;
    return false;
  }

  public bool TryGetMailbox(MeshIdentityId id, out MeshMailbox mailbox)
  {
    if (Mailboxes.TryGetValue(id.Value, out mailbox!))
    {
      return true;
    }

    mailbox = null!;
    return false;
  }

  internal static string PacketKey(PacketId id) => id.Value.ToString("N");
}
