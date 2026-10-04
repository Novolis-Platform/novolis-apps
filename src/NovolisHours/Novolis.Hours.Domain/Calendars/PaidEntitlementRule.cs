namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive contribution describing paid entitlement, not actual work or payment calculation.</summary>
public sealed record PaidEntitlementRule : DayRule
{
    /// <summary>Initializes a paid entitlement contribution.</summary>
    public PaidEntitlementRule(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Duration = duration;
    }

    /// <summary>Entitled duration represented by this rule.</summary>
    public TimeSpan Duration { get; }
}
