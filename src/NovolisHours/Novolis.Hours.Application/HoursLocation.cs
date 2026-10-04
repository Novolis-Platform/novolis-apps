namespace Novolis.Hours.Application;

/// <summary>A country location that supplies the baseline public calendar and time zone.</summary>
public sealed record HoursLocation(
    string Id,
    string Title,
    string CountryCode,
    string TimeZoneId,
    string CalendarSummary,
    string TemplateId);
