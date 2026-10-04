using System.Collections.Immutable;
using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Deterministic Dimension projection for one resolved workday.</summary>
public sealed record DimensionEvaluation
{
    /// <summary>Initializes an evaluation result.</summary>
    public DimensionEvaluation(
        ResolvedWorkDay workDay,
        IEnumerable<DimensionMeasure> measures,
        IEnumerable<string> missingCoverage)
    {
        ArgumentNullException.ThrowIfNull(workDay);
        ArgumentNullException.ThrowIfNull(measures);
        ArgumentNullException.ThrowIfNull(missingCoverage);
        WorkDay = workDay;
        Measures = measures.ToImmutableArray();
        MissingCoverage = missingCoverage
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Resolved workday being described.</summary>
    public ResolvedWorkDay WorkDay { get; }

    /// <summary>Derived and manual measures.</summary>
    public ImmutableArray<DimensionMeasure> Measures { get; }

    /// <summary>Definitions requiring coverage that remain partially uncovered.</summary>
    public ImmutableArray<string> MissingCoverage { get; }

    /// <summary>Actual work remains authoritative and unchanged by classification.</summary>
    public TimeSpan ActualWorked => WorkDay.ActualWorked;
}
