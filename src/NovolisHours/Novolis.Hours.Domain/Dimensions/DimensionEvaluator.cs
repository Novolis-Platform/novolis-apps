using System.Collections.Immutable;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Evaluates typed derived rules and append-only manual allocations.</summary>
public sealed class DimensionEvaluator
{
    /// <summary>
    /// Evaluates one resolved workday. Classification failures are configuration/data
    /// failures; the resolved workday itself remains valid and unchanged.
    /// </summary>
    public DimensionEvaluation Evaluate(
        ResolvedWorkDay workDay,
        DimensionConfiguration configuration,
        IEnumerable<DimensionAssignment>? assignments = null)
    {
        ArgumentNullException.ThrowIfNull(workDay);
        ArgumentNullException.ThrowIfNull(configuration);

        var measures = new List<DimensionMeasure>();
        foreach (var registration in configuration.Rules)
        {
            var context = new DimensionContext(workDay, configuration, measures);
            var produced = registration.Rule.Evaluate(context)
                ?? throw new InvalidOperationException(
                    $"Dimension rule '{registration.Id}' returned no result collection.");
            foreach (var measure in produced)
            {
                var normalized = NormalizeDerivedMeasure(measure, registration.Provenance);
                ValidateMeasure(normalized, configuration, DimensionMeasureSource.Derived);
                ApplyMeasure(measures, normalized, configuration);
            }
        }

        var activeAssignments = ResolveActiveAssignments(
            assignments ?? [],
            workDay.Key);
        foreach (var assignment in activeAssignments)
        {
            var definition = configuration.GetDefinition(assignment.DimensionId)
                ?? throw new InvalidOperationException(
                    $"Dimension '{assignment.DimensionId}' is not configured.");
            if (definition.AssignmentMode == DimensionAssignmentMode.Derived)
            {
                throw new InvalidOperationException(
                    $"Dimension '{assignment.DimensionId}' does not accept manual assignments.");
            }

            foreach (var interval in assignment.Intervals)
            {
                if (!IsCoveredByWork(interval, workDay.WorkedIntervals))
                {
                    throw new InvalidOperationException(
                        $"Assignment '{assignment.Id}' extends beyond resolved work.");
                }

                var measure = new DimensionMeasure(
                    assignment.DimensionId,
                    assignment.ValueId,
                    interval.Duration,
                    interval,
                    DimensionMeasureSource.Manual);
                ValidateMeasure(measure, configuration, DimensionMeasureSource.Manual);
                ApplyMeasure(measures, measure, configuration);
            }
        }

        var missingCoverage = configuration.Definitions
            .Where(definition => definition.RequireFullCoverage)
            .Where(definition => !HasFullCoverage(definition.Id, measures, workDay))
            .Select(definition => definition.Id);

        return new DimensionEvaluation(workDay, measures, missingCoverage);
    }

    private static DimensionMeasure NormalizeDerivedMeasure(
        DimensionMeasure measure,
        RuleRef provenance)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (measure.Source != DimensionMeasureSource.Derived)
        {
            throw new InvalidOperationException(
                $"A configured Dimension rule returned source '{measure.Source}'.");
        }

        return measure.Rule is null
            ? new DimensionMeasure(
                measure.DimensionId,
                measure.ValueId,
                measure.Duration,
                measure.Interval,
                DimensionMeasureSource.Derived,
                provenance)
            : measure;
    }

    private static void ValidateMeasure(
        DimensionMeasure measure,
        DimensionConfiguration configuration,
        DimensionMeasureSource expectedSource)
    {
        if (measure.Source != expectedSource)
        {
            throw new InvalidOperationException(
                $"Expected a {expectedSource} Dimension measure.");
        }

        var definition = configuration.GetDefinition(measure.DimensionId)
            ?? throw new InvalidOperationException(
                $"Dimension '{measure.DimensionId}' is not configured.");
        if (!definition.HasValue(measure.ValueId))
        {
            throw new InvalidOperationException(
                $"Value '{measure.ValueId}' is not configured on Dimension '{measure.DimensionId}'.");
        }
    }

    private static void ApplyMeasure(
        List<DimensionMeasure> measures,
        DimensionMeasure incoming,
        DimensionConfiguration configuration)
    {
        var definition = configuration.GetDefinition(incoming.DimensionId)!;
        if (definition.Cardinality == DimensionCardinality.Additive)
        {
            measures.Add(incoming);
            return;
        }

        var replacements = new List<DimensionMeasure>();
        foreach (var existing in measures)
        {
            if (!existing.DimensionId.Equals(
                    incoming.DimensionId,
                    StringComparison.Ordinal))
            {
                replacements.Add(existing);
                continue;
            }

            if (incoming.Interval is null || existing.Interval is null)
            {
                continue;
            }

            replacements.AddRange(Subtract(existing, incoming.Interval));
        }

        replacements.Add(incoming);
        measures.Clear();
        measures.AddRange(replacements);
    }

    private static IEnumerable<DimensionMeasure> Subtract(
        DimensionMeasure existing,
        WorkInterval cut)
    {
        var interval = existing.Interval!;
        if (!interval.Overlaps(cut))
        {
            yield return existing;
            yield break;
        }

        if (interval.Start < cut.Start)
        {
            var leftEnd = interval.End < cut.Start ? interval.End : cut.Start;
            if (leftEnd > interval.Start)
            {
                yield return Rebuild(existing, new WorkInterval(interval.Start, leftEnd));
            }
        }

        if (cut.End < interval.End)
        {
            var rightStart = interval.Start > cut.End ? interval.Start : cut.End;
            if (interval.End > rightStart)
            {
                yield return Rebuild(existing, new WorkInterval(rightStart, interval.End));
            }
        }
    }

    private static DimensionMeasure Rebuild(
        DimensionMeasure source,
        WorkInterval interval) =>
        new(
            source.DimensionId,
            source.ValueId,
            interval.Duration,
            interval,
            source.Source,
            source.Rule);

    private static ImmutableArray<DimensionAssignment> ResolveActiveAssignments(
        IEnumerable<DimensionAssignment> assignments,
        WorkDayKey workDay)
    {
        var ordered = assignments
            .Where(assignment => assignment.WorkDay == workDay)
            .OrderBy(assignment => assignment.RecordedAt)
            .ThenBy(assignment => assignment.Id)
            .ToImmutableArray();
        var superseded = ordered
            .Where(assignment => assignment.CorrectsAssignmentId.HasValue)
            .Select(assignment => assignment.CorrectsAssignmentId!.Value)
            .ToHashSet();
        return ordered
            .Where(assignment => !superseded.Contains(assignment.Id))
            .ToImmutableArray();
    }

    private static bool IsCoveredByWork(
        WorkInterval assignment,
        ImmutableArray<WorkInterval> worked)
    {
        var covered = TimeSpan.Zero;
        foreach (var interval in worked)
        {
            var start = assignment.Start > interval.Start
                ? assignment.Start
                : interval.Start;
            var end = assignment.End < interval.End ? assignment.End : interval.End;
            if (end > start)
            {
                covered += end - start;
            }
        }

        return covered >= assignment.Duration;
    }

    private static bool HasFullCoverage(
        string dimensionId,
        IReadOnlyList<DimensionMeasure> measures,
        ResolvedWorkDay workDay) =>
        !workDay.WorkedIntervals.Any(interval =>
            !IsCoveredByMeasures(
                interval,
                measures.Where(measure =>
                    measure.DimensionId.Equals(
                        dimensionId,
                        StringComparison.Ordinal) &&
                    measure.Interval is not null)));

    private static bool IsCoveredByMeasures(
        WorkInterval source,
        IEnumerable<DimensionMeasure> measures)
    {
        var covered = TimeSpan.Zero;
        foreach (var measure in measures)
        {
            var interval = measure.Interval!;
            var start = source.Start > interval.Start ? source.Start : interval.Start;
            var end = source.End < interval.End ? source.End : interval.End;
            if (end > start)
            {
                covered += end - start;
            }
        }

        return covered >= source.Duration;
    }
}
