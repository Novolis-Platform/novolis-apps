using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Report of temporal associations, not causal employee conclusions.</summary>
public sealed record BusinessPressureReport(
    ImmutableArray<BusinessPressureRow> Rows);
