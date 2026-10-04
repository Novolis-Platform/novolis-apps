using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>
/// Typed rule mapping neutral routine differences to configured Dimension values.
/// It does not decide whether a difference is overtime, flex, or misconduct.
/// </summary>
public sealed class RoutineDifferenceDimensionRule : IDimensionRule
{
    /// <summary>Initializes a routine-difference mapping.</summary>
    public RoutineDifferenceDimensionRule(
        string dimensionId,
        string outsideRoutineValueId,
        string? missingRoutineValueId,
        RuleRef? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outsideRoutineValueId);
        DimensionId = dimensionId;
        OutsideRoutineValueId = outsideRoutineValueId;
        MissingRoutineValueId = missingRoutineValueId;
        Provenance = provenance;
    }

    /// <summary>Dimension identity.</summary>
    public string DimensionId { get; }

    /// <summary>Value used for observed outside-routine work.</summary>
    public string OutsideRoutineValueId { get; }

    /// <summary>Optional value used for an unobserved routine duration.</summary>
    public string? MissingRoutineValueId { get; }

    /// <summary>Optional provenance.</summary>
    public RuleRef? Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<DimensionMeasure> Evaluate(DimensionContext context)
    {
        var result = new List<DimensionMeasure>();
        if (context.WorkDay.RoutineComparison.OutsideRoutine > TimeSpan.Zero)
        {
            foreach (var interval in OutsideRoutineSegments(context.WorkDay))
            {
                result.Add(new DimensionMeasure(
                    DimensionId,
                    OutsideRoutineValueId,
                    interval.Duration,
                    interval,
                    DimensionMeasureSource.Derived,
                    Provenance));
            }
        }

        if (MissingRoutineValueId is not null &&
            context.WorkDay.RoutineComparison.MissingRoutine > TimeSpan.Zero)
        {
            result.Add(new DimensionMeasure(
                DimensionId,
                MissingRoutineValueId,
                -context.WorkDay.RoutineComparison.MissingRoutine,
                null,
                DimensionMeasureSource.Derived,
                Provenance));
        }

        return result;
    }

    private static IEnumerable<WorkInterval> OutsideRoutineSegments(
        ResolvedWorkDay workDay)
    {
        var remaining = workDay.WorkedIntervals.ToList();
        foreach (var routine in workDay.Shape.RoutineWork)
        {
            var next = new List<WorkInterval>();
            foreach (var candidate in remaining)
            {
                var routineInterval = ToAbsoluteInterval(
                    workDay.Key.NominalDate,
                    routine,
                    workDay.Shape.TimeZoneId);
                if (!candidate.Overlaps(routineInterval))
                {
                    next.Add(candidate);
                    continue;
                }

                if (candidate.Start < routineInterval.Start)
                {
                    next.Add(new WorkInterval(
                        candidate.Start,
                        candidate.End < routineInterval.Start
                            ? candidate.End
                            : routineInterval.Start));
                }

                if (candidate.End > routineInterval.End)
                {
                    next.Add(new WorkInterval(
                        candidate.Start > routineInterval.End
                            ? candidate.Start
                            : routineInterval.End,
                        candidate.End));
                }
            }

            remaining = next;
        }

        return remaining.Where(interval => interval.Duration > TimeSpan.Zero);
    }

    private static WorkInterval ToAbsoluteInterval(
        DateOnly date,
        LocalTimeRange range,
        string timeZoneId)
    {
        var zone = FindTimeZone(timeZoneId);
        var endDate = range.End <= range.Start ? date.AddDays(1) : date;
        return new WorkInterval(
            ResolveLocal(zone, date, range.Start),
            ResolveLocal(zone, endDate, range.End));
    }

    private static DateTimeOffset ResolveLocal(
        TimeZoneInfo zone,
        DateOnly date,
        TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals(
            "Europe/Oslo",
            StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
