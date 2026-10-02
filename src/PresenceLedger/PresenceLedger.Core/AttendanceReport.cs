using System.Globalization;
using System.Text;

namespace PresenceLedger.Core;

/// <summary>Arrivals and departures for every place across retained days.</summary>
public sealed record AttendanceReport
{
    /// <summary>Creates a report.</summary>
    public AttendanceReport(IReadOnlyList<PlaceAttendance> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        Places = places;
    }

    /// <summary>Places that have at least one stay, ordered by name.</summary>
    public IReadOnlyList<PlaceAttendance> Places { get; }

    /// <summary>Pairs confirmed events into stays. The latest place name is used.</summary>
    public static AttendanceReport Build(
        IEnumerable<PresenceEvent> events,
        IEnumerable<TrackedLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(locations);

        var names = locations
            .GroupBy(location => location.Id)
            .ToDictionary(group => group.Key, group => group.Last().DisplayName);
        var places = events
            .GroupBy(item => item.LocationId)
            .Select(group => Pair(group.Key, Name(names, group.Key), group))
            .Where(place => place.Visits.Count > 0)
            .OrderBy(place => place.DisplayName, StringComparer.Ordinal)
            .ThenBy(place => place.LocationId)
            .ToArray();
        return new AttendanceReport(places);
    }

    /// <summary>Local-day lines for every stay, in time order.</summary>
    public IReadOnlyList<AttendanceMark> Marks(TimeZoneInfo zone, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var marks = new List<AttendanceMark>();
        foreach (var place in Places)
        {
            foreach (var visit in place.Visits)
                AddMarks(marks, visit, zone, asOf);
        }

        return marks
            .OrderBy(mark => mark.Day)
            .ThenBy(mark => mark.At)
            .ThenBy(mark => mark.Order)
            .ThenBy(mark => mark.Place, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Plain-text report for a place-by-place reading and a day-by-day reading.</summary>
    public string Format(DateTimeOffset asOf, TimeZoneInfo zone, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(culture);
        var localAsOf = TimeZoneInfo.ConvertTime(asOf, zone);
        var text = new StringBuilder();
        text.AppendLine("Presence Ledger");
        text.Append("Arrivals and departures through ");
        text.AppendLine(localAsOf.ToString("d MMM yyyy HH:mm", culture));
        text.AppendLine();

        if (Places.Count == 0)
        {
            text.AppendLine("No arrivals or departures yet.");
            return text.ToString();
        }

        foreach (var place in Places)
        {
            text.AppendLine(place.DisplayName);
            text.AppendLine(Summary(place, zone, asOf));
            DateOnly? current = null;
            foreach (var mark in Marks(zone, asOf).Where(mark => mark.LocationId == place.LocationId))
            {
                if (current != mark.Day)
                {
                    current = mark.Day;
                    text.AppendLine(mark.Day.ToString("ddd d MMM yyyy", culture));
                }

                text.Append("  ");
                text.AppendLine(mark.Line);
            }

            text.AppendLine();
        }

        text.AppendLine("By day");
        DateOnly? day = null;
        foreach (var mark in Marks(zone, asOf))
        {
            if (day != mark.Day)
            {
                day = mark.Day;
                text.AppendLine(mark.Day.ToString("ddd d MMM yyyy", culture));
            }

            text.Append("  ");
            text.Append(mark.Place);
            text.Append(" · ");
            text.AppendLine(mark.Line);
        }

        return text.ToString();
    }

    /// <summary>Short totals for one place.</summary>
    public static string Summary(PlaceAttendance place, TimeZoneInfo zone, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(zone);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Count(place.VisitCount, "visit", "visits")} · {Count(place.DayCount(zone, asOf), "day", "days")} · {Duration(place.TotalTime(asOf))}");
    }

    /// <summary>Hours and minutes, dropping seconds.</summary>
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;

        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}h {minutes:00}m")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m");
    }

    /// <summary>Local dates from the start of a stay through its end.</summary>
    public static IEnumerable<DateOnly> DaysTouched(
        PlaceVisit visit,
        TimeZoneInfo zone,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(zone);
        var startAt = visit.ArrivedAt ?? visit.LeftAt!.Value;
        var endAt = visit.LeftAt ?? (asOf > startAt ? asOf : startAt);
        var start = LocalDate(startAt, zone);
        var end = LocalDate(endAt, zone);
        if (end < start)
            end = start;

        for (var day = start; day <= end; day = day.AddDays(1))
            yield return day;
    }

    static PlaceAttendance Pair(
        Guid locationId,
        string displayName,
        IEnumerable<PresenceEvent> events)
    {
        PlaceVisit? open = null;
        var visits = new List<PlaceVisit>();
        foreach (var presenceEvent in events
                     .OrderBy(item => item.At)
                     .ThenBy(item => item.Transition == PresenceTransition.Arrived ? 0 : 1)
                     .ThenBy(item => item.EventId))
        {
            if (presenceEvent.Transition == PresenceTransition.Arrived)
            {
                open ??= new PlaceVisit(locationId, displayName, presenceEvent.At, null);
                continue;
            }

            if (open is not null)
            {
                visits.Add(open with { LeftAt = presenceEvent.At });
                open = null;
                continue;
            }

            visits.Add(new PlaceVisit(locationId, displayName, null, presenceEvent.At));
        }

        if (open is not null)
            visits.Add(open);

        return new PlaceAttendance(locationId, displayName, visits);
    }

    static void AddMarks(
        ICollection<AttendanceMark> marks,
        PlaceVisit visit,
        TimeZoneInfo zone,
        DateTimeOffset asOf)
    {
        if (visit.ArrivedAt is { } arrived)
        {
            marks.Add(new AttendanceMark(
                LocalDate(arrived, zone),
                arrived,
                0,
                visit.LocationId,
                visit.DisplayName,
                "arrived " + Clock(arrived, zone)));
        }

        if (visit.LeftAt is { } left)
        {
            marks.Add(new AttendanceMark(
                LocalDate(left, zone),
                left,
                2,
                visit.LocationId,
                visit.DisplayName,
                "left " + Clock(left, zone) + " · " + Duration(visit.DurationUntil(asOf))));
            return;
        }

        if (visit.ArrivedAt is not { } stillFrom)
            return;

        var line = "still there · " + Duration(visit.DurationUntil(asOf));
        marks.Add(new AttendanceMark(
            LocalDate(stillFrom, zone),
            stillFrom,
            1,
            visit.LocationId,
            visit.DisplayName,
            line));
        var today = LocalDate(asOf, zone);
        var arrivalDay = LocalDate(stillFrom, zone);
        if (today > arrivalDay)
        {
            marks.Add(new AttendanceMark(
                today,
                asOf,
                1,
                visit.LocationId,
                visit.DisplayName,
                line));
        }
    }

    static string Clock(DateTimeOffset at, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    static DateOnly LocalDate(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    static string Name(IReadOnlyDictionary<Guid, string> names, Guid locationId) =>
        names.TryGetValue(locationId, out var name) ? name : "Unknown place";

    static string Count(int value, string singular, string plural) =>
        value == 1 ? "1 " + singular : value.ToString(CultureInfo.InvariantCulture) + " " + plural;
}
