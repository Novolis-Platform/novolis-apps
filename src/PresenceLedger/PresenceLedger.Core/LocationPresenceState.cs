using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Persisted operational state, distinct from historical events.</summary>
public sealed record LocationPresenceState(
    Guid LocationId,
    PresenceState State,
    PresenceCandidate? CandidatePresent,
    AbsenceCandidate? CandidateAbsent,
    DateTimeOffset? LastEvidenceAt)
{
    /// <summary>Creates an empty state for a configured location.</summary>
    public static LocationPresenceState CreateAbsent(Guid locationId) =>
        new(locationId, PresenceState.Absent, null, null, null);
}
