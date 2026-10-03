namespace Novolis.Hours.Domain.Work;

/// <summary>An observed interval of actual work using absolute timestamps.</summary>
public sealed record WorkInterval
{
    /// <summary>Initializes an interval and enforces a positive duration.</summary>
    public WorkInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(end),
                end,
                "A work interval must end after it starts.");
        }

        Start = start;
        End = end;
    }

    /// <summary>Absolute start instant.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Absolute end instant.</summary>
    public DateTimeOffset End { get; }

    /// <summary>Elapsed duration of the interval.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Returns whether this interval overlaps another interval.</summary>
    public bool Overlaps(WorkInterval other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    /// <summary>Returns the overlap with another interval, if any.</summary>
    public WorkInterval? Intersection(WorkInterval other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Overlaps(other))
        {
            return null;
        }

        var start = Start > other.Start ? Start : other.Start;
        var end = End < other.End ? End : other.End;
        return new WorkInterval(start, end);
    }
}
