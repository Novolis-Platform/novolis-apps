using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Ordered sparse composition of effective work-calendar layers.</summary>
public sealed class WorkCalendarStack : IWorkCalendarStack
{
    private readonly ImmutableArray<WorkCalendarLayer> calendars;

    /// <summary>Initializes a work-calendar stack.</summary>
    public WorkCalendarStack(IEnumerable<WorkCalendarLayer> calendars, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        this.calendars = calendars
            .OrderBy(calendar => calendar.Order)
            .ThenBy(calendar => calendar.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        TimeZoneId = timeZoneId;
    }

    /// <summary>Ordered layers retained for configuration inspection.</summary>
    public ImmutableArray<WorkCalendarLayer> Calendars => calendars;

    /// <summary>Time-zone identifier for local calendar times.</summary>
    public string TimeZoneId { get; }

    /// <inheritdoc />
    public DayShape GetDayShape(DateOnly date)
    {
        var applied = ImmutableArray.CreateBuilder<AppliedDayRule>();
        var tags = ImmutableArray.CreateBuilder<DayTag>();
        var isWorkingDay = false;
        var expectedWork = TimeSpan.Zero;
        var paidEntitlement = TimeSpan.Zero;
        LocalTimeRange? workEnvelope = null;
        var coreHours = ImmutableArray<LocalTimeRange>.Empty;
        var routineWork = ImmutableArray<LocalTimeRange>.Empty;

        foreach (var calendar in calendars)
        {
            if (!calendar.AppliesTo(date))
            {
                continue;
            }

            foreach (var selector in calendar.Rules)
            {
                foreach (var rule in selector.GetRules(date))
                {
                    var appliedRule = new AppliedDayRule(
                        GetRuleId(selector, rule, date),
                        calendar.Id,
                        calendar.Version,
                        calendar.Order,
                        rule,
                        calendar.Source)
                    {
                        LayerKind = calendar.Kind,
                    };
                    if (selector is PublicHolidayCalendarRule holidayRule)
                    {
                        appliedRule = appliedRule with
                        {
                            Provenance = holidayRule.GetProvenance(date),
                        };
                    }

                    applied.Add(appliedRule);

                    switch (rule)
                    {
                        case WorkingDayRule workingDay:
                            isWorkingDay = workingDay.Value;
                            break;
                        case ExpectedWorkRule expected:
                            expectedWork = expected.Duration;
                            break;
                        case PaidEntitlementRule paid:
                            paidEntitlement = paid.Duration;
                            break;
                        case WorkEnvelopeRule envelope:
                            workEnvelope = envelope.Range;
                            break;
                        case CoreHoursRule core:
                            coreHours = core.Ranges;
                            break;
                        case RoutineWorkRule routine:
                            routineWork = routine.Ranges;
                            break;
                        case DayTagRule tag:
                            tags.Add(tag.Tag);
                            break;
                    }
                }
            }
        }

        return new DayShape(
            date,
            isWorkingDay,
            expectedWork,
            paidEntitlement,
            workEnvelope,
            coreHours,
            routineWork,
            tags,
            applied,
            TimeZoneId);
    }

    private static string GetRuleId(IWorkCalendarRule selector, DayRule rule, DateOnly date) =>
        selector switch
        {
            EveryDateCalendarRule every => every.Id,
            WeekdayCalendarRule weekday => weekday.Id,
            FixedDateCalendarRule fixedDate => fixedDate.Id,
            DateRangeCalendarRule range => range.Id,
            PublicHolidayCalendarRule holiday => holiday.GetProvenance(date)?.HolidayId ?? holiday.Id,
            PublicHolidayObservanceCalendarRule observance => observance.Id,
            ExcludingDatesCalendarRule excluding => excluding.Id,
            WeekBasedScheduleRule week => week.Id,
            _ => $"{selector.GetType().Name}:{rule.GetType().Name}",
        };
}
