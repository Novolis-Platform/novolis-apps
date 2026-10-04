using System.Collections.Immutable;
using Novolis.Hours.Domain.Compliance;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Aggregates compliance indicators without ranking people.</summary>
public sealed class HealthConcernsProjector
{
    /// <summary>Builds a report retaining every contributing WorkDay reference.</summary>
    public HealthConcernsReport Project(IEnumerable<ComplianceIndicator> indicators)
    {
        ArgumentNullException.ThrowIfNull(indicators);
        var concerns = indicators
            .GroupBy(
                indicator => (indicator.Code, indicator.Category))
            .Select(group => new HealthConcernAggregate(
                group.Key.Code,
                group.Key.Category,
                group.Count(),
                group.Aggregate(
                    TimeSpan.Zero,
                    (total, indicator) =>
                        total + (indicator.AffectedInterval?.Duration ?? TimeSpan.Zero)),
                group.Select(indicator => indicator.WorkDay)
                    .Distinct()
                    .ToImmutableArray()))
            .OrderBy(concern => concern.Category, StringComparer.Ordinal)
            .ThenBy(concern => concern.Code, StringComparer.Ordinal)
            .ToImmutableArray();
        return new HealthConcernsReport(concerns);
    }
}
