using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Immutable expected shape of one nominal local date.</summary>
public sealed record DayShape
{
    /// <summary>Initializes a date shape.</summary>
    public DayShape(
        DateOnly date,
        bool isWorkingDay,
        TimeSpan expectedWork,
        TimeSpan paidEntitlement,
        LocalTimeRange? workEnvelope,
        IEnumerable<LocalTimeRange> coreHours,
        IEnumerable<LocalTimeRange> routineWork,
        IEnumerable<DayTag> tags,
        IEnumerable<AppliedDayRule> rules,
        string timeZoneId = "UTC")
    {
        if (expectedWork < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWork));
        }

        if (paidEntitlement < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(paidEntitlement));
        }

        ArgumentNullException.ThrowIfNull(coreHours);
        ArgumentNullException.ThrowIfNull(routineWork);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        Date = date;
        IsWorkingDay = isWorkingDay;
        ExpectedWork = expectedWork;
        PaidEntitlement = paidEntitlement;
        WorkEnvelope = workEnvelope;
        CoreHours = coreHours.ToImmutableArray();
        RoutineWork = routineWork.ToImmutableArray();
        Tags = tags.ToImmutableArray();
        Rules = rules.ToImmutableArray();
        TimeZoneId = timeZoneId;
    }

    /// <summary>Nominal local date.</summary>
    public DateOnly Date { get; }

    /// <summary>Whether the resolved date is expected to be worked.</summary>
    public bool IsWorkingDay { get; }

    /// <summary>Expected worked duration for comparison.</summary>
    public TimeSpan ExpectedWork { get; }

    /// <summary>Paid entitlement metadata, not a payment calculation.</summary>
    public TimeSpan PaidEntitlement { get; }

    /// <summary>Optional local-clock working envelope.</summary>
    public LocalTimeRange? WorkEnvelope { get; }

    /// <summary>Local core-hours ranges.</summary>
    public ImmutableArray<LocalTimeRange> CoreHours { get; }

    /// <summary>Local routine ranges used by scheduled registrations.</summary>
    public ImmutableArray<LocalTimeRange> RoutineWork { get; }

    /// <summary>Additive semantic labels.</summary>
    public ImmutableArray<DayTag> Tags { get; }

    /// <summary>Ordered contributions used to calculate this shape.</summary>
    public ImmutableArray<AppliedDayRule> Rules { get; }

    /// <summary>Time-zone identifier used to resolve local routine ranges.</summary>
    public string TimeZoneId { get; }
}
