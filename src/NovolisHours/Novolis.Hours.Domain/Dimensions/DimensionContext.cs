using System.Collections.Immutable;
using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Input available to one typed Dimension rule.</summary>
public sealed record DimensionContext
{
    /// <summary>Initializes a rule context.</summary>
    public DimensionContext(
        ResolvedWorkDay workDay,
        DimensionConfiguration configuration,
        IEnumerable<DimensionMeasure> previouslyAppliedMeasures)
    {
        ArgumentNullException.ThrowIfNull(workDay);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(previouslyAppliedMeasures);
        WorkDay = workDay;
        Configuration = configuration;
        PreviouslyAppliedMeasures = previouslyAppliedMeasures.ToImmutableArray();
    }

    /// <summary>Resolved work being classified.</summary>
    public ResolvedWorkDay WorkDay { get; }

    /// <summary>Effective definitions and rules.</summary>
    public DimensionConfiguration Configuration { get; }

    /// <summary>Earlier derived measures in deterministic order.</summary>
    public ImmutableArray<DimensionMeasure> PreviouslyAppliedMeasures { get; }
}
