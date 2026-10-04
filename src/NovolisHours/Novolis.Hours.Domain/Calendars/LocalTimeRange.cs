namespace Novolis.Hours.Domain.Calendars;

/// <summary>Local clock range used by a routine, envelope, or core-hours rule.</summary>
public sealed record LocalTimeRange
{
    /// <summary>Initializes a non-empty local range; an end before start crosses midnight.</summary>
    public LocalTimeRange(TimeOnly start, TimeOnly end)
    {
        if (start == end)
        {
            throw new ArgumentException("A local time range must not be empty.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>Local clock start.</summary>
    public TimeOnly Start { get; }

    /// <summary>Local clock end.</summary>
    public TimeOnly End { get; }

    /// <summary>Duration, including a next-day crossing when End precedes Start.</summary>
    public TimeSpan Duration =>
        End > Start
            ? End - Start
            : (TimeSpan.FromDays(1) - Start.ToTimeSpan()) + End.ToTimeSpan();
}
