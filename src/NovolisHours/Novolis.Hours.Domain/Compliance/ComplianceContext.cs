using System.Collections.Immutable;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Dimensions;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Read-only context available to a compliance rule.</summary>
public sealed record ComplianceContext
{
    /// <summary>Initializes a compliance context.</summary>
    public ComplianceContext(
        ResolvedWorkDay workDay,
        DimensionEvaluation? dimensions = null,
        IEnumerable<ResolvedWorkDay>? surroundingWorkDays = null)
    {
        ArgumentNullException.ThrowIfNull(workDay);
        WorkDay = workDay;
        Dimensions = dimensions;
        SurroundingWorkDays = (surroundingWorkDays ?? []).ToImmutableArray();
    }

    /// <summary>Workday being evaluated.</summary>
    public ResolvedWorkDay WorkDay { get; }

    /// <summary>Optional classified meaning.</summary>
    public DimensionEvaluation? Dimensions { get; }

    /// <summary>Nearby resolved workdays for rest and trend rules.</summary>
    public ImmutableArray<ResolvedWorkDay> SurroundingWorkDays { get; }
}
