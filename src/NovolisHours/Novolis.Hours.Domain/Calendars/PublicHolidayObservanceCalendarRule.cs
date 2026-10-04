using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>
/// Organisation opinion that generated public holidays are not worked.
/// National holiday tags remain additive and are not replaced.
/// </summary>
public sealed class PublicHolidayObservanceCalendarRule : IWorkCalendarRule
{
    private readonly ImmutableHashSet<DateOnly> holidays;
    private readonly ImmutableArray<DayRule> rules;

    /// <summary>Initializes an observance selector for generated holiday dates.</summary>
    public PublicHolidayObservanceCalendarRule(
        string id,
        IEnumerable<GeneratedPublicHoliday> holidays,
        TimeSpan paidEntitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(holidays);
        if (paidEntitlement < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(paidEntitlement));
        }

        Id = id;
        this.holidays = holidays.Select(holiday => holiday.Date).ToImmutableHashSet();
        rules =
        [
            new WorkingDayRule(false),
            new ExpectedWorkRule(TimeSpan.Zero),
            new PaidEntitlementRule(paidEntitlement),
        ];
    }

    /// <summary>Stable selector identifier.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public IReadOnlyList<DayRule> GetRules(DateOnly date) =>
        holidays.Contains(date) ? rules : [];
}
