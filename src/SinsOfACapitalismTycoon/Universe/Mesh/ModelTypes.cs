using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>Durable mesh relay node (one per star system in the campaign seed).</summary>
internal sealed record MeshNode(
  MeshNodeId Id,
  string SystemId,
  string Name,
  int PulseBandwidthPerHour);

/// <summary>Directed hop with separate pulse vs bulk travel times.</summary>
internal sealed record MeshEdge(
  MeshNodeId From,
  MeshNodeId To,
  int PulseTravelHours,
  int BulkTravelHours,
  double DistanceLy);

/// <summary>Destination of a publish.</summary>
internal sealed record MeshAddress(
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
internal sealed record MeshPacket(
  PacketId Id,
  MeshTrafficLayer Layer,
  bool Sealed,
  ImmutableArray<byte> SignatureBlob,
  int Priority,
  /// <summary>Earliest universal hours-after-publish when the packet may be removed from the mesh entirely. Null = no global expiry.</summary>
  int? GlobalTtlHours,
  /// <summary>Hours after a node receives the packet before that node may drop it from its local cache. Null = no local expiry.</summary>
  int? LocalTtlHours,
  /// <summary>Retention priority for local cache eviction (higher kept longer under pressure).</summary>
  int LocalRetentionPriority,
  MeshNodeId OriginNode,
  MeshAddress Destination,
  long PublishedHour,
  string Subject = "",
  string Body = "",
  string Topic = "",
  /// <summary>Stable logical id for offers / retractions (e.g. spot job key). Empty when unused.</summary>
  string LogicalKey = "");

/// <summary>Per-node cache membership: when received + local retention priority snapshot.</summary>
internal sealed record NodeCacheEntry(
  long ReceivedHour,
  int LocalPriority,
  int? LocalTtlHours = null);

/// <summary>
/// Mailbox for a person, household, firm, ship, or thing — parked at a star-system node.
/// Identity-addressed packets <b>push</b> here only while co-located with a node that holds them.
/// <see cref="LinkedToNode"/> is false while the hull is in FTL (corridor Underway) — no pull/push.
/// </summary>
internal sealed record MeshMailbox(
  MeshIdentityId Owner,
  MeshIdentityKind Kind,
  MeshNodeId LocationNodeId,
  ImmutableHashSet<string> PushedPacketKeys,
  bool LinkedToNode = true);

/// <summary>
/// Feed subscriptions (Atom/RSS-style). <see cref="MeshFeedId.Emergency"/> is always effective
/// even if missing from <see cref="FeedIds"/>.
/// </summary>
internal sealed record MeshSubscriptionBook(
  MeshIdentityId Owner,
  ImmutableHashSet<string> FeedIds);

/// <summary>Disposable pulse/bulk carrier in transit.</summary>
internal sealed record InFlightDrone(
  DroneId Id,
  PacketId PacketId,
  MeshNodeId From,
  MeshNodeId To,
  int RemainingHours,
  ImmutableArray<MeshNodeId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Bandwidth-gated launch waiting at a node.</summary>
internal sealed record PendingLaunch(
  PacketId PacketId,
  MeshNodeId From,
  MeshNodeId To,
  ImmutableArray<MeshNodeId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Counters for reports and tests.</summary>
internal sealed record MeshStats(
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
  long BandwidthDeferred = 0,
  long LocalCacheDrops = 0,
  long GlobalPacketDrops = 0,
  long RetractionsApplied = 0);
