namespace Novolis.Hours.Domain.Calendars;

/// <summary>Frozen public-holiday input generated outside runtime resolution.</summary>
public sealed record GeneratedPublicHoliday(
    DateOnly Date,
    string HolidayId,
    string Name,
    CalendarRuleProvenance Provenance);
