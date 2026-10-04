using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Neutral organisation-level health-pattern projection.</summary>
public sealed record HealthConcernsReport(
    ImmutableArray<HealthConcernAggregate> Concerns);
