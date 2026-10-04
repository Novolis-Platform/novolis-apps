using System.Globalization;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Human clock and date copy used by every Hours surface.</summary>
public static class HoursClock
{
    /// <summary>Formats a duration as <c>7:30</c> or <c>+4:15</c>.</summary>
    public static string Format(TimeSpan duration, bool signed = false)
    {
        var sign = duration < TimeSpan.Zero ? "-" : signed && duration > TimeSpan.Zero ? "+" : string.Empty;
        var absolute = duration.Duration();
        var hours = (int)absolute.TotalHours;
        return $"{sign}{hours}:{absolute.Minutes:00}";
    }

    /// <summary>Formats a local clock as <c>08:00</c>.</summary>
    public static string Format(TimeOnly time) => time.ToString("HH\\:mm", CultureInfo.InvariantCulture);

    /// <summary>Formats a range as <c>08:00–11:30</c>.</summary>
    public static string Format(TimeOnly start, TimeOnly end) => $"{Format(start)}–{Format(end)}";

    /// <summary>Formats a weekday-first date in English, independent of OS locale.</summary>
    public static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM", CultureInfo.GetCultureInfo("en-GB"));

    /// <summary>Builds a zone-aware timestamp for a nominal Hours day.</summary>
    public static DateTimeOffset ToNominalTimestamp(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var zone = ResolveZone(timeZoneId);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static TimeZoneInfo ResolveZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            var windowsId = timeZoneId switch
            {
                "Europe/Oslo" => "W. Europe Standard Time",
                "Europe/Paris" => "Romance Standard Time",
                "Europe/Warsaw" => "Central European Standard Time",
                "Europe/Helsinki" => "FLE Standard Time",
                _ => "W. Europe Standard Time",
            };
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
