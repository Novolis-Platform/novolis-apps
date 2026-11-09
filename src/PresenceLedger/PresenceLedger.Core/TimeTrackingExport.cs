using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PresenceLedger.Core;

/// <summary>Portable time-entry exports for Hours and Clockify.</summary>
public static class TimeTrackingExport
{
    static readonly JsonSerializerOptions HoursJson = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Clockify time-entry CSV. Place name is Description; optional project overrides
    /// the Project column, otherwise the place name is reused.
    /// </summary>
    public static string ToClockifyCsv(
        AttendanceReport report,
        TimeZoneInfo zone,
        DateTimeOffset asOf,
        string? projectName = null,
        bool billable = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(zone);

        var text = new StringBuilder();
        text.AppendLine(
            "Project,Client,Description,Task,User,Email,Billable,Start Date,Start Time,End Date,End Time,Duration (h),Tags");

        foreach (var visit in ClosedVisits(report, asOf))
        {
            var start = TimeZoneInfo.ConvertTime(visit.ArrivedAt!.Value, zone);
            var end = TimeZoneInfo.ConvertTime(visit.LeftAt!.Value, zone);
            var duration = end - start;
            if (duration <= TimeSpan.Zero)
                continue;

            var project = string.IsNullOrWhiteSpace(projectName)
                ? visit.DisplayName
                : projectName.Trim();
            text.Append(Csv(project));
            text.Append(',');
            text.Append(Csv(string.Empty));
            text.Append(',');
            text.Append(Csv(visit.DisplayName));
            text.Append(',');
            text.Append(Csv(string.Empty));
            text.Append(',');
            text.Append(Csv(string.Empty));
            text.Append(',');
            text.Append(Csv(string.Empty));
            text.Append(',');
            text.Append(Csv(billable ? "Yes" : "No"));
            text.Append(',');
            text.Append(Csv(start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(start.ToString("HH:mm", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(end.ToString("HH:mm", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(FormatClockifyDuration(duration)));
            text.Append(',');
            text.Append(Csv("presence-ledger"));
            text.AppendLine();
        }

        return text.ToString();
    }

    /// <summary>
    /// Hours-oriented presence visit document. Schema is local-first and stable so
    /// Hours can import confirmed stays without depending on Presence Ledger types.
    /// </summary>
    public static string ToHoursJson(
        AttendanceReport report,
        TimeZoneInfo zone,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(zone);

        var visits = ClosedVisits(report, asOf)
            .Select(visit =>
            {
                var start = TimeZoneInfo.ConvertTime(visit.ArrivedAt!.Value, zone);
                var end = TimeZoneInfo.ConvertTime(visit.LeftAt!.Value, zone);
                return new HoursPresenceVisit(
                    visit.LocationId,
                    visit.DisplayName,
                    start,
                    end,
                    Math.Max(0, (int)Math.Round((end - start).TotalMinutes)));
            })
            .ToArray();

        var document = new HoursPresenceDocument(
            "novolis.hours.presence-visits.v1",
            zone.Id,
            asOf,
            visits);
        return JsonSerializer.Serialize(document, HoursJson);
    }

    /// <summary>Flat CSV companion for Hours / spreadsheet import.</summary>
    public static string ToHoursCsv(
        AttendanceReport report,
        TimeZoneInfo zone,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(zone);

        var text = new StringBuilder();
        text.AppendLine("date,place,arrivedLocal,leftLocal,durationMinutes,locationId,timeZone");
        foreach (var visit in ClosedVisits(report, asOf))
        {
            var start = TimeZoneInfo.ConvertTime(visit.ArrivedAt!.Value, zone);
            var end = TimeZoneInfo.ConvertTime(visit.LeftAt!.Value, zone);
            var minutes = Math.Max(0, (int)Math.Round((end - start).TotalMinutes));
            text.Append(Csv(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(visit.DisplayName));
            text.Append(',');
            text.Append(Csv(start.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(end.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(minutes.ToString(CultureInfo.InvariantCulture)));
            text.Append(',');
            text.Append(Csv(visit.LocationId.ToString("D")));
            text.Append(',');
            text.Append(Csv(zone.Id));
            text.AppendLine();
        }

        return text.ToString();
    }

    static IEnumerable<PlaceVisit> ClosedVisits(AttendanceReport report, DateTimeOffset asOf) =>
        report.Places
            .SelectMany(place => place.Visits)
            .Where(visit => visit.ArrivedAt is not null && visit.LeftAt is not null)
            .Where(visit => visit.LeftAt!.Value > visit.ArrivedAt!.Value)
            .Where(visit => visit.LeftAt!.Value <= asOf)
            .OrderBy(visit => visit.ArrivedAt)
            .ThenBy(visit => visit.DisplayName, StringComparer.Ordinal);

    static string FormatClockifyDuration(TimeSpan duration)
    {
        var totalMinutes = (int)Math.Round(duration.TotalMinutes);
        if (totalMinutes < 0)
            totalMinutes = 0;

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}");
    }

    static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        return value;
    }

    sealed record HoursPresenceDocument(
        string Schema,
        string TimeZone,
        DateTimeOffset ExportedAt,
        IReadOnlyList<HoursPresenceVisit> Visits);

    sealed record HoursPresenceVisit(
        Guid LocationId,
        string Place,
        DateTimeOffset ArrivedAt,
        DateTimeOffset LeftAt,
        int DurationMinutes);
}
