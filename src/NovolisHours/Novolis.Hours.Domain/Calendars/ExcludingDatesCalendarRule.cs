using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Wraps a selector so exclusive employment opinions stay silent on excluded dates.</summary>
public sealed class ExcludingDatesCalendarRule : IWorkCalendarRule
{
    private readonly ImmutableHashSet<DateOnly> excluded;
    private readonly IWorkCalendarRule inner;

    /// <summary>Initializes a sparse exclusion wrapper.</summary>
    public ExcludingDatesCalendarRule(
        string id,
        IWorkCalendarRule inner,
        IEnumerable<DateOnly> excludedDates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(excludedDates);

        Id = id;
        this.inner = inner;
        excluded = excludedDates.ToImmutableHashSet();
    }

    /// <summary>Stable selector identifier.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) =>
        excluded.Contains(date) ? [] : inner.GetRules(date);
}
