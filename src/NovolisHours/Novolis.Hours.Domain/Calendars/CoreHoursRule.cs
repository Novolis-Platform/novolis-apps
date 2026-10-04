using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive contribution describing required core/office-hour ranges.</summary>
public sealed record CoreHoursRule : DayRule
{
    /// <summary>Initializes core-hour ranges.</summary>
    public CoreHoursRule(IEnumerable<LocalTimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        Ranges = ranges.ToImmutableArray();
    }

    /// <summary>Ranges during which presence is expected.</summary>
    public ImmutableArray<LocalTimeRange> Ranges { get; }
}
