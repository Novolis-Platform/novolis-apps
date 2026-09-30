using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Transient arrival evidence that can survive process restart.</summary>
public sealed record PresenceCandidate(
    DateTimeOffset? StartedAt,
    DateTimeOffset? FirstInsideAt,
    DateTimeOffset? FirstMatchingWifiAt,
    DateTimeOffset LastEvidenceAt,
    int PositionObservations,
    int MatchingWifiObservations);
