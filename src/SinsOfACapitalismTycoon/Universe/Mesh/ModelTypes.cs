using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Durable mesh relay node (one per star system in the campaign seed).</summary>
public sealed record MeshNode(
  MeshNodeId Id,
  string SystemId,
  string Name,
  int PulseBandwidthPerHour);

/// <summary>Directed hop with separate pulse vs bulk travel times.</summary>
public sealed record MeshEdge(
  MeshNodeId From,
  MeshNodeId To,
  int PulseTravelHours,
  int BulkTravelHours,
  double DistanceLy);

/// <summary>Destination of a publish.</summary>
public sealed record MeshAddress(
  MeshAddressKind Kind,
  MeshNodeId? Place = null,
  MeshIdentityId? Identity = null,
  MeshFeedId? Feed = null)
{
  public static MeshAddress ToPlace(MeshNodeId node) => new(MeshAddressKind.Place, Place: node);

  public static MeshAddress ToIdentity(MeshIdentityId id) =>
    new(MeshAddressKind.Identity, Identity: id);

  public static MeshAddress ToFeed(MeshFeedId feed) => new(MeshAddressKind.Feed, Feed: feed);
}

/// <summary>Published signed object (signature opaque in BM).</summary>
public sealed record MeshPacket(
  PacketId Id,
  MeshTrafficLayer Layer,
  bool Sealed,
  ImmutableArray<byte> SignatureBlob,
  int Priority,
  int? TtlHours,
  MeshNodeId OriginNode,
  MeshAddress Destination,
  long PublishedHour);

/// <summary>
/// Mailbox for a person, household, firm, ship, or thing — parked at a star-system node.
/// Identity-addressed packets <b>push</b> here only while co-located with a node that holds them.
/// </summary>
public sealed record MeshMailbox(
  MeshIdentityId Owner,
  MeshIdentityKind Kind,
  MeshNodeId LocationNodeId,
  ImmutableHashSet<string> PushedPacketKeys);

/// <summary>
/// Feed subscriptions (Atom/RSS-style). <see cref="MeshFeedId.Emergency"/> is always effective
/// even if missing from <see cref="FeedIds"/>.
/// </summary>
public sealed record MeshSubscriptionBook(
  MeshIdentityId Owner,
  ImmutableHashSet<string> FeedIds);

/// <summary>Disposable pulse/bulk carrier in transit.</summary>
public sealed record InFlightDrone(
  DroneId Id,
  PacketId PacketId,
  MeshNodeId From,
  MeshNodeId To,
  int RemainingHours,
  ImmutableArray<MeshNodeId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Bandwidth-gated launch waiting at a node.</summary>
public sealed record PendingLaunch(
  PacketId PacketId,
  MeshNodeId From,
  MeshNodeId To,
  ImmutableArray<MeshNodeId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Counters for reports and tests.</summary>
public sealed record MeshStats(
  long DirectedPublishes = 0,
  long IdentityPublishes = 0,
  long FeedPublishes = 0,
  long DronesLaunched = 0,
  long DronesArrived = 0,
  long DronesLost = 0,
  long CacheCredits = 0,
  long MailboxPushes = 0,
  long FeedPulls = 0,
  long EmergencyForced = 0,
  long BandwidthDeferred = 0);
