namespace PresenceLedger.Core;

/// <summary>A presence interval clipped to one displayed local calendar day.</summary>
public sealed record PresenceInterval(
    Guid LocationId,
    string DisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    PresenceConfidence Confidence,
    bool IsOpen);
