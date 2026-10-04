using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>One weekday's working opinion used by an employee week calendar.</summary>
public sealed class HoursWeekday
{
    /// <summary>Initializes a weekday payload.</summary>
    public HoursWeekday(
        bool isWorkingDay,
        TimeSpan expectedWork,
        LocalTimeRange? workEnvelope,
        IEnumerable<LocalTimeRange> coreHours,
        IEnumerable<LocalTimeRange> routineWork)
    {
        if (expectedWork < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWork));
        }

        ArgumentNullException.ThrowIfNull(coreHours);
        ArgumentNullException.ThrowIfNull(routineWork);

        IsWorkingDay = isWorkingDay;
        ExpectedWork = expectedWork;
        WorkEnvelope = workEnvelope;
        CoreHours = coreHours.ToImmutableArray();
        RoutineWork = routineWork.ToImmutableArray();
    }

    /// <summary>Whether the weekday is a working day.</summary>
    public bool IsWorkingDay { get; }

    /// <summary>Expected worked duration for the weekday.</summary>
    public TimeSpan ExpectedWork { get; }

    /// <summary>Optional local-clock envelope.</summary>
    public LocalTimeRange? WorkEnvelope { get; }

    /// <summary>Required core/office-hour ranges.</summary>
    public ImmutableArray<LocalTimeRange> CoreHours { get; }

    /// <summary>Configured routine intervals.</summary>
    public ImmutableArray<LocalTimeRange> RoutineWork { get; }
}
