using Novolis.Time.Week;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Emits DayRules from a week-based employee calendar; silence when the week has no opinion.</summary>
public sealed class WeekBasedScheduleRule : IWorkCalendarRule
{
    private readonly WeekBasedCalendar<HoursWeekday> calendar;

    /// <summary>Initializes a week-based schedule selector.</summary>
    public WeekBasedScheduleRule(string id, WeekBasedCalendar<HoursWeekday> calendar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(calendar);
        Id = id;
        this.calendar = calendar;
    }

    /// <summary>Stable rule identifier used in diagnostics.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date)
    {
        var resolved = calendar.Resolve(date);
        if (!resolved.HasValue || resolved.Value is null)
        {
            return [];
        }

        var day = resolved.Value;
        var rules = new List<DayRule>
        {
            new WorkingDayRule(day.IsWorkingDay),
            new ExpectedWorkRule(day.ExpectedWork),
        };
        if (!day.IsWorkingDay)
        {
            rules.Add(new PaidEntitlementRule(TimeSpan.Zero));
        }

        if (day.WorkEnvelope is { } envelope)
        {
            rules.Add(new WorkEnvelopeRule(envelope));
        }

        if (day.CoreHours.Length > 0)
        {
            rules.Add(new CoreHoursRule(day.CoreHours));
        }

        if (day.RoutineWork.Length > 0)
        {
            rules.Add(new RoutineWorkRule(day.RoutineWork));
        }

        return rules;
    }
}
