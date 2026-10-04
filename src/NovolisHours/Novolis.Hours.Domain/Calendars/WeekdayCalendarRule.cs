using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Calendar rule that contributes on selected ISO weekdays.</summary>
public sealed class WeekdayCalendarRule : ICalendarRule
{
    private readonly ImmutableHashSet<DayOfWeek> weekdays;
    private readonly ImmutableArray<DayRule> rules;

    /// <summary>Initializes a weekday selector.</summary>
    public WeekdayCalendarRule(
        string id,
        IEnumerable<DayOfWeek> weekdays,
        IEnumerable<DayRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(weekdays);
        ArgumentNullException.ThrowIfNull(rules);

        Id = id;
        this.weekdays = weekdays.ToImmutableHashSet();
        this.rules = rules.ToImmutableArray();
    }

    /// <summary>Stable rule identifier used in diagnostics.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) =>
        weekdays.Contains(date.DayOfWeek) ? rules : [];
}
