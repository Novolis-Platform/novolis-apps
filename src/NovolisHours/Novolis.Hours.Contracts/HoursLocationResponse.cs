namespace Novolis.Hours.Contracts;

/// <summary>Country location that sets the baseline public calendar.</summary>
public sealed record HoursLocationResponse(
    string Id,
    string Title,
    string CountryCode,
    string TimeZoneId,
    string CalendarSummary);
