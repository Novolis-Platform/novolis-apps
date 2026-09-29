using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>A user-defined location whose evidence can be evaluated independently.</summary>
public sealed record TrackedLocation
{
    /// <summary>Creates a tracked location.</summary>
    public TrackedLocation(
        Guid id,
        string displayName,
        GeoCircle area,
        WifiEvidence? wifi,
        PresencePolicy policy,
        DateTimeOffset? effectiveFromUtc = null,
        DateTimeOffset? effectiveToUtc = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Location id must not be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (effectiveFromUtc is { } from
            && effectiveToUtc is { } to
            && to <= from)
            throw new ArgumentException(
                "The location validity end must be after its start.",
                nameof(effectiveToUtc));

        DisplayName = displayName.Trim();
        Id = id;
        Area = area;
        Wifi = wifi;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        EffectiveFromUtc = effectiveFromUtc?.ToUniversalTime();
        EffectiveToUtc = effectiveToUtc?.ToUniversalTime();
    }

    /// <summary>Stable location identifier.</summary>
    public Guid Id { get; }

    /// <summary>User-facing name with no special semantic meaning.</summary>
    public string DisplayName { get; }

    /// <summary>Configured geographic area.</summary>
    public GeoCircle Area { get; }

    /// <summary>Optional connected-network corroboration.</summary>
    public WifiEvidence? Wifi { get; }

    /// <summary>Inference policy for this location.</summary>
    public PresencePolicy Policy { get; }

    /// <summary>UTC instant at which this location revision becomes effective.</summary>
    public DateTimeOffset? EffectiveFromUtc { get; }

    /// <summary>UTC instant at which this location revision stops being effective.</summary>
    public DateTimeOffset? EffectiveToUtc { get; }
}

/// <summary>Configured Wi-Fi evidence for a location.</summary>
public sealed record WifiEvidence
{
    /// <summary>Creates Wi-Fi evidence for a display SSID.</summary>
    public WifiEvidence(string ssid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssid);
        Ssid = ssid.Trim();
    }

    /// <summary>Expected connected SSID.</summary>
    public string Ssid { get; }
}

/// <summary>Policy controlling confirmation, departure, and evidence continuity.</summary>
public sealed record PresencePolicy
{
    /// <summary>Creates a policy using the standard evidence gap.</summary>
    public PresencePolicy(
        TimeSpan confirmationDuration,
        TimeSpan departureDuration)
        : this(
            confirmationDuration,
            departureDuration,
            TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>Creates an inference policy.</summary>
    [JsonConstructor]
    public PresencePolicy(
        TimeSpan confirmationDuration,
        TimeSpan departureDuration,
        TimeSpan maximumEvidenceGap)
    {
        if (confirmationDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(confirmationDuration));

        if (departureDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(departureDuration));

        if (maximumEvidenceGap <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumEvidenceGap));

        ConfirmationDuration = confirmationDuration;
        DepartureDuration = departureDuration;
        MaximumEvidenceGap = maximumEvidenceGap;
    }

    /// <summary>Required sustained duration before arrival is confirmed.</summary>
    public TimeSpan ConfirmationDuration { get; }

    /// <summary>Required sustained duration before departure is confirmed.</summary>
    public TimeSpan DepartureDuration { get; }

    /// <summary>Maximum gap between qualifying observations in one evidence chain.</summary>
    public TimeSpan MaximumEvidenceGap { get; }
}

/// <summary>Standard policies used by the first application flow.</summary>
public static class PresencePolicyDefaults
{
    /// <summary>Three-minute corroborated confirmation and departure.</summary>
    public static readonly PresencePolicy Standard = new(
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(3));

    /// <summary>Stronger location-only confirmation policy.</summary>
    public static readonly PresencePolicy LocationOnly = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(3));
}

/// <summary>A transient platform observation.</summary>
public abstract record Observation(DateTimeOffset At);

/// <summary>A location sample with reported horizontal uncertainty.</summary>
public sealed record PositionObservation : Observation
{
    /// <summary>Creates a position observation.</summary>
    public PositionObservation(
        DateTimeOffset at,
        GeoCoordinate position,
        double accuracyMeters)
        : base(at)
    {
        if (!double.IsFinite(accuracyMeters) || accuracyMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(accuracyMeters));

        Position = position;
        AccuracyMeters = accuracyMeters;
    }

    /// <summary>Observed position.</summary>
    public GeoCoordinate Position { get; }

    /// <summary>Reported horizontal accuracy in meters.</summary>
    public double AccuracyMeters { get; }
}

/// <summary>A connected-network observation; null means the platform had no SSID value.</summary>
public sealed record WifiObservation(
    DateTimeOffset At,
    string? ConnectedSsid)
    : Observation(At);

/// <summary>Semantic transition stored in the event ledger.</summary>
public enum PresenceTransition
{
    /// <summary>Presence was confirmed.</summary>
    Arrived,

    /// <summary>Departure was confirmed.</summary>
    Left,
}

/// <summary>Confidence attached to a durable presence event.</summary>
public enum PresenceConfidence
{
    /// <summary>Evidence satisfied the configured policy.</summary>
    Confirmed,
}

/// <summary>Evidence summary for a durable event.</summary>
public sealed record PresenceEvidence
{
    /// <summary>Creates event evidence.</summary>
    public PresenceEvidence(PresenceConfidence confidence, TimeSpan confirmationDuration)
    {
        if (confirmationDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(confirmationDuration));

        Confidence = confidence;
        ConfirmationDuration = confirmationDuration;
    }

    /// <summary>Evidence confidence.</summary>
    public PresenceConfidence Confidence { get; }

    /// <summary>Duration satisfied before the event was emitted.</summary>
    public TimeSpan ConfirmationDuration { get; }
}

/// <summary>Durable semantic presence event.</summary>
public sealed record PresenceEvent
{
    /// <summary>Creates an event.</summary>
    public PresenceEvent(
        Guid eventId,
        Guid locationId,
        PresenceTransition transition,
        DateTimeOffset at,
        PresenceEvidence evidence)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Event id must not be empty.", nameof(eventId));

        if (locationId == Guid.Empty)
            throw new ArgumentException("Location id must not be empty.", nameof(locationId));

        EventId = eventId;
        LocationId = locationId;
        Transition = transition;
        At = at;
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    /// <summary>Stable event identifier.</summary>
    public Guid EventId { get; }

    /// <summary>Location affected by the event.</summary>
    public Guid LocationId { get; }

    /// <summary>Arrival or departure.</summary>
    public PresenceTransition Transition { get; }

    /// <summary>Retrospective inferred time.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Summary of the evidence chain.</summary>
    public PresenceEvidence Evidence { get; }
}

/// <summary>Operational state of one configured location.</summary>
public enum PresenceState
{
    /// <summary>No confirmed presence and no active arrival candidate.</summary>
    Absent,

    /// <summary>Qualifying evidence is accumulating for arrival.</summary>
    CandidatePresent,

    /// <summary>Arrival has been confirmed.</summary>
    Present,

    /// <summary>Evidence is accumulating for departure.</summary>
    CandidateAbsent,
}

/// <summary>Transient arrival evidence that can survive process restart.</summary>
public sealed record PresenceCandidate(
    DateTimeOffset? StartedAt,
    DateTimeOffset? FirstInsideAt,
    DateTimeOffset? FirstMatchingWifiAt,
    DateTimeOffset LastEvidenceAt,
    int PositionObservations,
    int MatchingWifiObservations);

/// <summary>Transient departure evidence that can survive process restart.</summary>
public sealed record AbsenceCandidate(
    DateTimeOffset StartedAt,
    DateTimeOffset LastEvidenceAt);

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

/// <summary>Result of applying one observation to one location.</summary>
public sealed record PresenceInferenceResult(
    LocationPresenceState State,
    PresenceEvent? Event);
