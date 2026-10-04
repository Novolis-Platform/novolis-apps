namespace Novolis.Hours.Domain.Calendars;

/// <summary>Exclusive contribution describing expected worked duration.</summary>
public sealed record ExpectedWorkRule : DayRule
{
    /// <summary>Rejects a negative expected duration.</summary>
    public ExpectedWorkRule(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Duration = duration;
    }

    /// <summary>Expected worked duration.</summary>
    public TimeSpan Duration { get; }
}
