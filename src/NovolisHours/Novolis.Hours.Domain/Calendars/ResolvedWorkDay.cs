using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Pure resolution of one work assertion against one DayShape.</summary>
public sealed record ResolvedWorkDay
{
    /// <summary>Initializes a resolved workday.</summary>
    public ResolvedWorkDay(
        WorkDayKey key,
        WorkRegistration effectiveRegistration,
        DayShape shape,
        IEnumerable<WorkInterval> workedIntervals,
        TimeSpan actualWorked,
        RoutineComparison routineComparison)
    {
        ArgumentNullException.ThrowIfNull(effectiveRegistration);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(workedIntervals);
        ArgumentNullException.ThrowIfNull(routineComparison);
        if (actualWorked < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(actualWorked));
        }

        Key = key;
        EffectiveRegistration = effectiveRegistration;
        Shape = shape;
        WorkedIntervals = workedIntervals.ToImmutableArray();
        ActualWorked = actualWorked;
        RoutineComparison = routineComparison;
    }

    /// <summary>Logical workday identity.</summary>
    public WorkDayKey Key { get; }

    /// <summary>Registration that supplied the effective assertion.</summary>
    public WorkRegistration EffectiveRegistration { get; }

    /// <summary>Calendar shape used for resolution.</summary>
    public DayShape Shape { get; }

    /// <summary>Absolute intervals used by downstream evaluation.</summary>
    public ImmutableArray<WorkInterval> WorkedIntervals { get; }

    /// <summary>Total resolved worked duration.</summary>
    public TimeSpan ActualWorked { get; }

    /// <summary>Neutral routine comparison.</summary>
    public RoutineComparison RoutineComparison { get; }
}
