using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Calendar rule that contributes over an inclusive local-date range.</summary>
public sealed class DateRangeCalendarRule : ICalendarRule
{
    private readonly ImmutableArray<DayRule> rules;

    /// <summary>Initializes an inclusive date range selector.</summary>
    public DateRangeCalendarRule(
        string id,
        DateOnly from,
        DateOnly to,
        IEnumerable<DayRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(rules);
        if (to < from)
        {
            throw new ArgumentException("The end date must not precede the start date.", nameof(to));
        }

        Id = id;
        From = from;
        To = to;
        this.rules = rules.ToImmutableArray();
    }

    /// <summary>Stable rule identifier used in diagnostics.</summary>
    public string Id { get; }

    /// <summary>Inclusive start date.</summary>
    public DateOnly From { get; }

    /// <summary>Inclusive end date.</summary>
    public DateOnly To { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) =>
        date >= From && date <= To ? rules : [];
}
