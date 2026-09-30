namespace PresenceLedger.Core;

/// <summary>Deterministic projection of retained evidence into a displayed day.</summary>
public sealed record PresenceDayProjection(
    DateOnly DisplayDate,
    TimeZoneInfo DisplayTimeZone,
    DateTimeOffset DisplayStartUtc,
    DateTimeOffset DisplayEndUtc,
    IReadOnlyList<PresenceInterval> Intervals,
    IReadOnlyList<PresenceObservationDebugPoint> Observations)
{
    /// <summary>Whether the projection has no retained evidence or semantic intervals.</summary>
    public bool IsEmpty => Intervals.Count == 0 && Observations.Count == 0;

    /// <summary>Number of intervals still open at the end of the displayed day.</summary>
    public int OpenIntervalCount => Intervals.Count(item => item.IsOpen);
}
