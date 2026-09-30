using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Transient departure evidence that can survive process restart.</summary>
public sealed record AbsenceCandidate(
    DateTimeOffset StartedAt,
    DateTimeOffset LastEvidenceAt);
