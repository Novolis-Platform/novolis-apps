using System.Collections.Immutable;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Reporting;

/// <summary>Attributes explicitly selected extra-work measures by temporal overlap.</summary>
public sealed class BusinessPressureProjector
{
    /// <summary>Projects one pressure Dimension value against one allocation Dimension.</summary>
    public BusinessPressureReport Project(
        IEnumerable<DimensionEvaluation> evaluations,
        string pressureDimensionId,
        string pressureValueId,
        string allocationDimensionId)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        ArgumentException.ThrowIfNullOrWhiteSpace(pressureDimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pressureValueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(allocationDimensionId);

        var contributions = new List<(string ValueId, TimeSpan Duration, WorkDayKey WorkDay)>();
        foreach (var evaluation in evaluations)
        {
            var pressureMeasures = evaluation.Measures
                .Where(measure =>
                    measure.DimensionId.Equals(
                        pressureDimensionId,
                        StringComparison.Ordinal) &&
                    measure.ValueId.Equals(
                        pressureValueId,
                        StringComparison.Ordinal) &&
                    measure.Duration > TimeSpan.Zero)
                .ToArray();
            var allocations = evaluation.Measures
                .Where(measure =>
                    measure.DimensionId.Equals(
                        allocationDimensionId,
                        StringComparison.Ordinal) &&
                    measure.Interval is not null)
                .ToArray();

            foreach (var pressure in pressureMeasures)
            {
                if (pressure.Interval is null)
                {
                    contributions.Add((
                        "Unattributed",
                        pressure.Duration,
                        evaluation.WorkDay.Key));
                    continue;
                }

                var attributed = TimeSpan.Zero;
                foreach (var allocation in allocations)
                {
                    var overlap = Overlap(pressure.Interval, allocation.Interval!);
                    if (overlap <= TimeSpan.Zero)
                    {
                        continue;
                    }

                    attributed += overlap;
                    contributions.Add((
                        allocation.ValueId,
                        overlap,
                        evaluation.WorkDay.Key));
                }

                if (attributed < pressure.Duration)
                {
                    contributions.Add((
                        "Unattributed",
                        pressure.Duration - attributed,
                        evaluation.WorkDay.Key));
                }
            }
        }

        var rows = contributions
            .GroupBy(contribution => contribution.ValueId, StringComparer.Ordinal)
            .Select(group => new BusinessPressureRow(
                allocationDimensionId,
                group.Key,
                group.Aggregate(
                    TimeSpan.Zero,
                    (total, contribution) => total + contribution.Duration),
                group.Select(contribution => contribution.WorkDay)
                    .Distinct()
                    .ToImmutableArray()))
            .OrderBy(row => row.ValueId, StringComparer.Ordinal)
            .ToImmutableArray();
        return new BusinessPressureReport(rows);
    }

    private static TimeSpan Overlap(WorkInterval left, WorkInterval right)
    {
        var start = left.Start > right.Start ? left.Start : right.Start;
        var end = left.End < right.End ? left.End : right.End;
        return end > start ? end - start : TimeSpan.Zero;
    }
}
