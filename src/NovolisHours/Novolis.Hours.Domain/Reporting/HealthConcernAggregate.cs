using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Organisation-level factual concern aggregate with drill-down references.</summary>
public sealed record HealthConcernAggregate(
    string Code,
    string Category,
    int IndicatorCount,
    TimeSpan AffectedDuration,
    ImmutableArray<WorkDayKey> SourceWorkDays);
