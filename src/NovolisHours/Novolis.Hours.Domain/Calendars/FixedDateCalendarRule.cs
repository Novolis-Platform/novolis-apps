using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Calendar rule that contributes on a fixed month/day each year.</summary>
public sealed class FixedDateCalendarRule : ICalendarRule
{
    private readonly ImmutableArray<DayRule> rules;

    /// <summary>Initializes a selector for one exact date.</summary>
    public FixedDateCalendarRule(
        string id,
        DateOnly date,
        IEnumerable<DayRule> rules)
        : this(id, date.Month, date.Day, rules)
    {
        Date = date;
    }

    /// <summary>Initializes a fixed month/day selector.</summary>
    public FixedDateCalendarRule(
        string id,
        int month,
        int day,
        IEnumerable<DayRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(rules);
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        if (day is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        Id = id;
        Month = month;
        Day = day;
        this.rules = rules.ToImmutableArray();
    }

    /// <summary>Stable rule identifier used in diagnostics.</summary>
    public string Id { get; }

    /// <summary>Month component.</summary>
    public int Month { get; }

    /// <summary>Day component.</summary>
    public int Day { get; }

    /// <summary>Exact date when this is a one-year rule; otherwise null.</summary>
    public DateOnly? Date { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) =>
        Date.HasValue
            ? date == Date.Value ? rules : []
            : date.Month == Month && date.Day == Day ? rules : [];
}
