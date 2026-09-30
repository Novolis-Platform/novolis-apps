using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

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
