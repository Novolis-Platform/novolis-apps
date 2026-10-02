namespace PresenceLedger.Core;

/// <summary>One stay at a place, from a confirmed arrival until departure.</summary>
public sealed record PlaceVisit
{
    /// <summary>Creates a stay. An open stay has no departure.</summary>
    public PlaceVisit(
        Guid locationId,
        string displayName,
        DateTimeOffset? arrivedAt,
        DateTimeOffset? leftAt)
    {
        if (arrivedAt is null && leftAt is null)
            throw new ArgumentException("A stay needs an arrival or a departure.");

        if (arrivedAt is { } arrived && leftAt is { } left && left < arrived)
            throw new ArgumentOutOfRangeException(nameof(leftAt), "Departure precedes arrival.");

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        LocationId = locationId;
        DisplayName = displayName.Trim();
        ArrivedAt = arrivedAt;
        LeftAt = leftAt;
    }

    /// <summary>Place this stay belongs to.</summary>
    public Guid LocationId { get; }

    /// <summary>Place name at the time the report was built.</summary>
    public string DisplayName { get; }

    /// <summary>Confirmed arrival, when one was retained.</summary>
    public DateTimeOffset? ArrivedAt { get; init; }

    /// <summary>Confirmed departure. Absent while the stay is open.</summary>
    public DateTimeOffset? LeftAt { get; init; }

    /// <summary>Time spent, counting an open stay through <paramref name="asOf"/>.</summary>
    public TimeSpan DurationUntil(DateTimeOffset asOf)
    {
        if (ArrivedAt is not { } arrived)
            return TimeSpan.Zero;

        var end = LeftAt ?? asOf;
        return end <= arrived ? TimeSpan.Zero : end - arrived;
    }
}
