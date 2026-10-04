namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive contribution describing whether the date is a working day.</summary>
public sealed record WorkingDayRule(bool Value) : DayRule;
