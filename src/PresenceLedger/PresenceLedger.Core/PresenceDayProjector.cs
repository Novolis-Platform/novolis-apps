namespace PresenceLedger.Core;

/// <summary>Builds a day projection without accessing storage or platform APIs.</summary>
public sealed class PresenceDayProjector
{
    /// <summary>
    /// Projects all supplied events and observations into one display-timezone day.
    /// Callers should supply events before the day as well as events inside it so an
    /// interval that began earlier can be rendered at midnight.
    /// </summary>
    public PresenceDayProjection Project(
        DateOnly displayDate,
        TimeZoneInfo displayTimeZone,
        IEnumerable<PresenceEvent> events,
        IEnumerable<PresenceObservationRecord> observations,
        IEnumerable<TrackedLocation> locationHistory)
    {
        ArgumentNullException.ThrowIfNull(displayTimeZone);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(locationHistory);

        var startLocal = displayDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = displayDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(startLocal, displayTimeZone),
            TimeSpan.Zero);
        var endUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(endLocal, displayTimeZone),
            TimeSpan.Zero);

        var locations = locationHistory
            .GroupBy(location => location.Id)
            .ToDictionary(group => group.Key, group => group.Last());

        var intervals = new List<PresenceInterval>();
        var active = new Dictionary<Guid, ActiveInterval>();
        foreach (var presenceEvent in events
                     .OrderBy(item => item.At)
                     .ThenBy(item => item.EventId))
        {
            var at = presenceEvent.At.ToUniversalTime();
            var location = ResolveLocation(locations, presenceEvent.LocationId);
            var displayName = location?.DisplayName ?? "Unknown location";

            if (presenceEvent.Transition == PresenceTransition.Arrived)
            {
                if (!active.ContainsKey(presenceEvent.LocationId))
                {
                    active[presenceEvent.LocationId] = new ActiveInterval(
                        at,
                        displayName,
                        presenceEvent.Evidence.Confidence);
                }

                continue;
            }

            if (!active.Remove(presenceEvent.LocationId, out var started))
                continue;

            AddClippedInterval(
                intervals,
                presenceEvent.LocationId,
                started.DisplayName,
                started.StartedAt,
                at,
                started.Confidence,
                startUtc,
                endUtc,
                isOpen: false);
        }

        foreach (var (locationId, started) in active)
        {
            AddClippedInterval(
                intervals,
                locationId,
                started.DisplayName,
                started.StartedAt,
                null,
                started.Confidence,
                startUtc,
                endUtc,
                isOpen: true);
        }

        var debugPoints = SsidPositionAnchor.Apply(locationHistory, observations)
            .Where(item => item.At >= startUtc && item.At < endUtc)
            .OrderBy(item => item.At)
            .Select(item => new PresenceObservationDebugPoint(
                item.At,
                item.Position,
                item.AccuracyMeters,
                item.WifiStatus,
                item.ConnectedSsid))
            .ToArray();

        return new PresenceDayProjection(
            displayDate,
            displayTimeZone,
            startUtc,
            endUtc,
            intervals
                .OrderBy(item => item.StartedAt)
                .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
                .ToArray(),
            debugPoints);
    }

    static TrackedLocation? ResolveLocation(
        IReadOnlyDictionary<Guid, TrackedLocation> locations,
        Guid locationId)
    {
        locations.TryGetValue(locationId, out var location);
        return location;
    }

    static void AddClippedInterval(
        ICollection<PresenceInterval> target,
        Guid locationId,
        string displayName,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        PresenceConfidence confidence,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        bool isOpen)
    {
        var effectiveStart = startedAt < dayStart ? dayStart : startedAt;
        var effectiveEnd = endedAt is null
            ? dayEnd
            : endedAt.Value > dayEnd
                ? dayEnd
                : endedAt.Value;

        if (effectiveEnd <= dayStart || effectiveStart >= dayEnd || effectiveEnd <= effectiveStart)
            return;

        var openAtDayEnd = isOpen || endedAt is null || endedAt >= dayEnd;
        target.Add(new PresenceInterval(
            locationId,
            displayName,
            effectiveStart,
            openAtDayEnd ? null : effectiveEnd,
            confidence,
            openAtDayEnd));
    }

    sealed record ActiveInterval(
        DateTimeOffset StartedAt,
        string DisplayName,
        PresenceConfidence Confidence);
}
