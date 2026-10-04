namespace Novolis.Hours.Domain.Calendars;

/// <summary>Resolves an ordered work-calendar stack into a date shape.</summary>
public interface IWorkCalendarStack
{
    /// <summary>Gets the immutable shape and complete applied-rule provenance.</summary>
    DayShape GetDayShape(DateOnly date);
}
