using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Neutral temporal association between extra work and a business Dimension.</summary>
public sealed record BusinessPressureRow(
    string DimensionId,
    string ValueId,
    TimeSpan AssociatedDuration,
    ImmutableArray<WorkDayKey> SourceWorkDays);
