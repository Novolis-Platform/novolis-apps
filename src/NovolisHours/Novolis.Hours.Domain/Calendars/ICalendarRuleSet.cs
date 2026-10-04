namespace Novolis.Hours.Domain.Calendars;

/// <summary>Resolves an ordered calendar stack into a date shape.</summary>
public interface ICalendarRuleSet
{
    /// <summary>Gets the immutable shape and complete applied-rule provenance.</summary>
    DayShape GetDayShape(DateOnly date);
}
