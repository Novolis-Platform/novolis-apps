using Novolis.Time.Workday;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Builds the frozen Workday calendar used by starter legal policies.</summary>
public static class PolicyCalendarFactory
{
    /// <summary>Creates a weekday-minus-holiday calendar from generated Workday facts.</summary>
    public static IWorkdayCalendar CreateOfficeCalendar(
        string id,
        string countryCode,
        TimeSpan paidEntitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        _ = paidEntitlement;
        return WorkdayCalendar.FromAllGeneratedHolidays(id, countryCode);
    }
}
