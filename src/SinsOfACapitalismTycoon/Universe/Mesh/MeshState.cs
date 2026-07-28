using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Immutable aggregate mesh state (stocks).</summary>
public sealed record MeshState(
  long HourIndex,
  MeshPolicy Policy,
  ImmutableDictionary<string, MeshHub> Hubs,
  ImmutableArray<MeshEdge> Edges,
  ImmutableDictionary<string, MeshPacket> Packets,
  ImmutableArray<InFlightDrone> Drones,
  ImmutableArray<PendingLaunch> Pending,
  /// <summary>HubId.Value → packet id strings visible at hub.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> HubCaches,
  /// <summary>IdentityId.Value → packet id strings in mailbox.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> Mailboxes,
  ImmutableDictionary<string, MeshIdentityBinding> Identities,
  /// <summary>HubId.Value → launches already consumed this hour.</summary>
  ImmutableDictionary<string, int> BandwidthUsedThisHour,
  /// <summary>PacketId → hubs that already initiated flood fan-out this visit.</summary>
  ImmutableDictionary<string, ImmutableHashSet<string>> FloodSeededAt,
  /// <summary>PacketKey → loss count (for MaxLossesPerPacket).</summary>
  ImmutableDictionary<string, int> PacketLossCounts,
  MeshStats Stats)
{
  public static MeshState Empty(MeshPolicy? policy = null) => new(
    HourIndex: 0,
    Policy: policy ?? new MeshPolicy(),
    Hubs: ImmutableDictionary<string, MeshHub>.Empty,
    Edges: ImmutableArray<MeshEdge>.Empty,
    Packets: ImmutableDictionary<string, MeshPacket>.Empty,
    Drones: ImmutableArray<InFlightDrone>.Empty,
    Pending: ImmutableArray<PendingLaunch>.Empty,
    HubCaches: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    Mailboxes: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    Identities: ImmutableDictionary<string, MeshIdentityBinding>.Empty,
    BandwidthUsedThisHour: ImmutableDictionary<string, int>.Empty,
    FloodSeededAt: ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
    PacketLossCounts: ImmutableDictionary<string, int>.Empty,
    Stats: new MeshStats());

  public bool IsVisibleAt(PacketId packet, MeshHubId hub) =>
    HubCaches.TryGetValue(hub.Value, out var set) && set.Contains(packet.Value.ToString("N"));

  public bool IsInMailbox(PacketId packet, MeshIdentityId identity) =>
    Mailboxes.TryGetValue(identity.Value, out var set) && set.Contains(packet.Value.ToString("N"));

  public bool TryGetPacket(PacketId id, out MeshPacket packet)
  {
    if (Packets.TryGetValue(id.Value.ToString("N"), out packet!))
    {
      return true;
    }

    packet = null!;
    return false;
  }

  internal static string PacketKey(PacketId id) => id.Value.ToString("N");
}
