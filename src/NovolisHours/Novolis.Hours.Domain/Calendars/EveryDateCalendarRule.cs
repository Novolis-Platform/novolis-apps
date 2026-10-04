using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Calendar rule that contributes on every selected date.</summary>
public sealed class EveryDateCalendarRule : IWorkCalendarRule
{
    private readonly ImmutableArray<DayRule> rules;

    /// <summary>Initializes an always-applicable rule.</summary>
    public EveryDateCalendarRule(string id, IEnumerable<DayRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        this.rules = rules.ToImmutableArray();
    }

    /// <summary>Stable rule identifier used in diagnostics.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) => rules;
}
