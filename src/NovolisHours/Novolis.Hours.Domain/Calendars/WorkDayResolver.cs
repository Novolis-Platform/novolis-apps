using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Pure resolver for scheduled and observed registrations.</summary>
public sealed class WorkDayResolver : IWorkRegistrationResolver
{
    /// <inheritdoc />
    public ResolvedWorkDay Resolve(WorkRegistration registration, DayShape shape)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(shape);
        if (registration.WorkDay.NominalDate != shape.Date)
        {
            throw new ArgumentException(
                "The registration and calendar shape must use the same nominal date.",
                nameof(shape));
        }

        var intervals = registration.Intent == WorkRecordIntent.WorkedAsScheduled
            ? ResolveRoutine(shape)
            : registration.Intervals
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.End)
                .ToImmutableArray();

        var actualWorked = intervals.Aggregate(
            TimeSpan.Zero,
            (total, interval) => total + interval.Duration);
        var routineIntervals = ResolveRoutine(shape);
        var comparison = Compare(
            intervals,
            routineIntervals,
            registration.Intent != WorkRecordIntent.WorkedAsScheduled);

        return new ResolvedWorkDay(
            registration.WorkDay,
            registration,
            shape,
            intervals,
            actualWorked,
            comparison);
    }

    private static ImmutableArray<WorkInterval> ResolveRoutine(DayShape shape)
    {
        if (!shape.IsWorkingDay || shape.RoutineWork.IsEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<WorkInterval>(shape.RoutineWork.Length);
        foreach (var range in shape.RoutineWork)
        {
            builder.Add(CreateAbsoluteInterval(shape.Date, range, shape.TimeZoneId));
        }

        return builder
            .OrderBy(interval => interval.Start)
            .ThenBy(interval => interval.End)
            .ToImmutableArray();
    }

    private static WorkInterval CreateAbsoluteInterval(
        DateOnly nominalDate,
        LocalTimeRange range,
        string timeZoneId)
    {
        var zone = FindTimeZone(timeZoneId);
        var endDate = range.End <= range.Start
            ? nominalDate.AddDays(1)
            : nominalDate;

        var start = ResolveLocal(zone, nominalDate, range.Start);
        var end = ResolveLocal(zone, endDate, range.End);
        if (end <= start)
        {
            throw new InvalidOperationException(
                $"The local routine {range.Start:HH\\:mm}-{range.End:HH\\:mm} does not resolve to a positive interval in {timeZoneId}.");
        }

        return new WorkInterval(start, end);
    }

    private static DateTimeOffset ResolveLocal(
        TimeZoneInfo zone,
        DateOnly date,
        TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            var before = zone.GetUtcOffset(local.AddHours(-2));
            var after = zone.GetUtcOffset(local.AddHours(2));
            var gap = after - before;
            local = local.Add(gap > TimeSpan.Zero ? gap : TimeSpan.FromHours(1));
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
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

    private static RoutineComparison Compare(
        ImmutableArray<WorkInterval> actual,
        ImmutableArray<WorkInterval> routine,
        bool includeSplitDifference)
    {
        var outside = actual.Aggregate(
            TimeSpan.Zero,
            (total, interval) => total + interval.Duration - OverlapWith(interval, routine));
        var coveredRoutine = routine.Aggregate(
            TimeSpan.Zero,
            (total, interval) => total + OverlapWith(interval, actual));
        var routineDuration = routine.Aggregate(
            TimeSpan.Zero,
            (total, interval) => total + interval.Duration);
        var missing = routineDuration - coveredRoutine;
        var differences = ImmutableArray.CreateBuilder<RoutineDifference>();

        if (outside > TimeSpan.Zero)
        {
            differences.Add(new RoutineDifference(
                RoutineDifferenceKind.OutsideRoutine,
                outside,
                "Observed work is outside the configured routine."));
        }

        if (missing > TimeSpan.Zero)
        {
            differences.Add(new RoutineDifference(
                RoutineDifferenceKind.MissingRoutine,
                missing,
                "Configured routine time has no corresponding observation."));
        }

        if (includeSplitDifference && actual.Length > 1)
        {
            differences.Add(new RoutineDifference(
                RoutineDifferenceKind.SplitWorkDay,
                actual.Aggregate(
                    TimeSpan.Zero,
                    (total, interval) => total + interval.Duration),
                "Observed work is represented by multiple intervals."));
        }

        if (!actual.IsEmpty && !routine.IsEmpty && actual[0].Start > routine[0].Start)
        {
            differences.Add(new RoutineDifference(
                RoutineDifferenceKind.LateStart,
                actual[0].Start - routine[0].Start,
                "Observed work starts after the configured routine."));
        }

        if (!actual.IsEmpty && !routine.IsEmpty && actual[^1].End < routine[^1].End)
        {
            differences.Add(new RoutineDifference(
                RoutineDifferenceKind.EarlyFinish,
                routine[^1].End - actual[^1].End,
                "Observed work ends before the configured routine."));
        }

        return new RoutineComparison(outside, missing, differences);
    }

    private static TimeSpan OverlapWith(
        WorkInterval source,
        ImmutableArray<WorkInterval> candidates)
    {
        var total = TimeSpan.Zero;
        foreach (var candidate in candidates)
        {
            var start = source.Start > candidate.Start ? source.Start : candidate.Start;
            var end = source.End < candidate.End ? source.End : candidate.End;
            if (end > start)
            {
                total += end - start;
            }
        }

        return total > source.Duration ? source.Duration : total;
    }
}
