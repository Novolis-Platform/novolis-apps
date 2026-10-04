using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Derived or assigned meaning attached to an interval or signed duration.</summary>
public sealed record DimensionMeasure
{
    /// <summary>Initializes a Dimension measure.</summary>
    public DimensionMeasure(
        string dimensionId,
        string valueId,
        TimeSpan duration,
        WorkInterval? interval,
        DimensionMeasureSource source,
        RuleRef? rule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueId);
        if (interval is not null && duration != interval.Duration)
        {
            throw new ArgumentException(
                "An interval measure duration must equal the interval duration.",
                nameof(duration));
        }

        DimensionId = dimensionId;
        ValueId = valueId;
        Duration = duration;
        Interval = interval;
        Source = source;
        Rule = rule;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Configured value identity.</summary>
    public string ValueId { get; }

    /// <summary>Signed duration; interval measures are positive.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Optional actual-work interval being described.</summary>
    public WorkInterval? Interval { get; }

    /// <summary>Measure origin.</summary>
    public DimensionMeasureSource Source { get; }

    /// <summary>Typed rule provenance for derived measures.</summary>
    public RuleRef? Rule { get; }
}
