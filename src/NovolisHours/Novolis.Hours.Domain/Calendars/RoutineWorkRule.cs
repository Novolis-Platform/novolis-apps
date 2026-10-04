using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive contribution describing the configured routine intervals.</summary>
public sealed record RoutineWorkRule : DayRule
{
    /// <summary>Initializes routine ranges.</summary>
    public RoutineWorkRule(IEnumerable<LocalTimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        Ranges = ranges.ToImmutableArray();
    }

    /// <summary>Routine intervals used by WorkedAsScheduled registrations.</summary>
    public ImmutableArray<LocalTimeRange> Ranges { get; }
}
