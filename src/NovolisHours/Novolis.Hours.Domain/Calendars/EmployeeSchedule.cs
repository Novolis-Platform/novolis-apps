using Novolis.Time.Week;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Employee weekly schedule: a week-based calendar plus the local time zone.</summary>
public sealed class EmployeeSchedule
{
    /// <summary>Initializes an employee week schedule.</summary>
    public EmployeeSchedule(
        string employeeId,
        string timeZoneId,
        WeekBasedCalendar<HoursWeekday> calendar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentNullException.ThrowIfNull(calendar);

        EmployeeId = employeeId;
        TimeZoneId = timeZoneId;
        Calendar = calendar;
    }

    /// <summary>Employee the schedule belongs to.</summary>
    public string EmployeeId { get; }

    /// <summary>IANA time-zone identifier for local weekday times.</summary>
    public string TimeZoneId { get; }

    /// <summary>Week pattern, cycle, and dated overrides.</summary>
    public WeekBasedCalendar<HoursWeekday> Calendar { get; }

    /// <summary>Resolves the weekday payload for a local date.</summary>
    public WeekDayResolution<HoursWeekday> Resolve(DateOnly date) => Calendar.Resolve(date);
}
