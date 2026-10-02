namespace PresenceLedger.Core;

/// <summary>Every retained stay at one place.</summary>
public sealed record PlaceAttendance
{
    /// <summary>Creates the attendance for one place.</summary>
    public PlaceAttendance(
        Guid locationId,
        string displayName,
        IReadOnlyList<PlaceVisit> visits)
    {
        ArgumentNullException.ThrowIfNull(visits);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        LocationId = locationId;
        DisplayName = displayName.Trim();
        Visits = visits;
    }

    /// <summary>Place these stays belong to.</summary>
    public Guid LocationId { get; }

    /// <summary>Place name at the time the report was built.</summary>
    public string DisplayName { get; }

    /// <summary>Stays in arrival order.</summary>
    public IReadOnlyList<PlaceVisit> Visits { get; }

    /// <summary>Closed and open stays.</summary>
    public int VisitCount => Visits.Count;

    /// <summary>Sum of stays, with an open stay measured through <paramref name="asOf"/>.</summary>
    public TimeSpan TotalTime(DateTimeOffset asOf)
    {
        var total = TimeSpan.Zero;
        foreach (var visit in Visits)
            total += visit.DurationUntil(asOf);

        return total;
    }

    /// <summary>Local calendar days a stay touched.</summary>
    public int DayCount(TimeZoneInfo zone, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var days = new HashSet<DateOnly>();
        foreach (var visit in Visits)
        {
            foreach (var day in AttendanceReport.DaysTouched(visit, zone, asOf))
                days.Add(day);
        }

        return days.Count;
    }
}
