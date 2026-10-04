using System.Collections.Immutable;
using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Indicators derived from a workday without changing the workday.</summary>
public sealed record ComplianceEvaluation
{
    /// <summary>Initializes an evaluation.</summary>
    public ComplianceEvaluation(
        ResolvedWorkDay workDay,
        IEnumerable<ComplianceIndicator> indicators)
    {
        ArgumentNullException.ThrowIfNull(workDay);
        ArgumentNullException.ThrowIfNull(indicators);
        WorkDay = workDay;
        Indicators = indicators.ToImmutableArray();
    }

    /// <summary>Input workday.</summary>
    public ResolvedWorkDay WorkDay { get; }

    /// <summary>Factual indicators.</summary>
    public ImmutableArray<ComplianceIndicator> Indicators { get; }
}
