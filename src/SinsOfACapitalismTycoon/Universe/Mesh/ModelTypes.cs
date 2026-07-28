using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Durable mesh node (star system / station hub).</summary>
public sealed record MeshHub(
  MeshHubId Id,
  string SystemId,
  string Name,
  int PulseBandwidthPerHour);

/// <summary>Directed hop with separate pulse vs bulk travel times.</summary>
public sealed record MeshEdge(
  MeshHubId From,
  MeshHubId To,
  int PulseTravelHours,
  int BulkTravelHours,
  double DistanceLy);

/// <summary>Destination of a publish.</summary>
public sealed record MeshAddress(
  MeshAddressKind Kind,
  MeshHubId? Place = null,
  MeshIdentityId? Identity = null)
{
  public static MeshAddress ToPlace(MeshHubId hub) => new(MeshAddressKind.Place, Place: hub);

  public static MeshAddress ToIdentity(MeshIdentityId id) =>
    new(MeshAddressKind.Identity, Identity: id);

  public static MeshAddress ToPublic() => new(MeshAddressKind.Public);
}

/// <summary>Published signed object (signature opaque in BM).</summary>
public sealed record MeshPacket(
  PacketId Id,
  MeshTrafficLayer Layer,
  bool Sealed,
  ImmutableArray<byte> SignatureBlob,
  int Priority,
  int? TtlHours,
  MeshHubId OriginHub,
  MeshAddress Destination,
  long PublishedHour);

/// <summary>Optional last-known hub biases flood order only — never upgrades SLA.</summary>
public sealed record MeshIdentityBinding(
  MeshIdentityId Id,
  MeshHubId? LastKnownHub = null);

/// <summary>Disposable pulse/bulk carrier in transit.</summary>
public sealed record InFlightDrone(
  DroneId Id,
  PacketId PacketId,
  MeshHubId From,
  MeshHubId To,
  int RemainingHours,
  ImmutableArray<MeshHubId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Bandwidth-gated launch waiting at a hub.</summary>
public sealed record PendingLaunch(
  PacketId PacketId,
  MeshHubId From,
  MeshHubId To,
  ImmutableArray<MeshHubId> RemainingPathAfterArrival,
  bool IsFloodHop,
  int Priority);

/// <summary>Counters for reports and tests (stocks/flows of interest).</summary>
public sealed record MeshStats(
  long DirectedPublishes = 0,
  long FloodPublishes = 0,
  long PublicPublishes = 0,
  long DronesLaunched = 0,
  long DronesArrived = 0,
  long DronesLost = 0,
  long CacheCredits = 0,
  long MailboxCredits = 0,
  long BandwidthDeferred = 0);
