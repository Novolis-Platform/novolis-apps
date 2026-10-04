using System.Collections.Immutable;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Monday-first week of DayShape tiles.</summary>
public sealed class WeekStudioModel
{
    /// <summary>Builds a week from projected days. Missing dates are omitted.</summary>
    public WeekStudioModel(IReadOnlyList<WorkDayResponse> days, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(days);
        Today = today;
        var ordered = days.OrderBy(day => day.NominalDate).ToArray();
        if (ordered.Length == 0)
        {
            WeekStart = MondayOnOrBefore(today);
            Tiles = [];
            return;
        }

        WeekStart = MondayOnOrBefore(ordered[0].NominalDate);
        var tiles = ImmutableArray.CreateBuilder<WeekTile>(ordered.Length);
        foreach (var day in ordered)
        {
            tiles.Add(new WeekTile(
                day.NominalDate,
                HoursClock.FormatDate(day.NominalDate).Split(' ')[0],
                HoursClock.Format(day.ExpectedWork),
                day.IsWorkingDay,
                day.Registration is not null,
                day.NominalDate == today,
                ChipFor(day),
                DetailFor(day),
                day));
        }

        Tiles = tiles.ToImmutable();
    }

    /// <summary>Monday of the displayed week.</summary>
    public DateOnly WeekStart { get; }

    /// <summary>Sunday of the displayed week.</summary>
    public DateOnly WeekEnd => WeekStart.AddDays(6);

    /// <summary>Reference "today" used for the open tile.</summary>
    public DateOnly Today { get; }

    /// <summary>Seven (or fewer) tiles in date order.</summary>
    public ImmutableArray<WeekTile> Tiles { get; }

    /// <summary>Sum of expected work across the week.</summary>
    public TimeSpan ExpectedWork =>
        Tiles.Aggregate(TimeSpan.Zero, (total, tile) => total + tile.Day.ExpectedWork);

    /// <summary>Sum of recorded actual work.</summary>
    public TimeSpan RecordedWork =>
        Tiles.Aggregate(TimeSpan.Zero, (total, tile) => total + tile.Day.ActualWorked);

    /// <summary>Working days with no registration.</summary>
    public int UnrecordedWorkingDays =>
        Tiles.Count(tile => tile.IsWorkingDay && !tile.HasRegistration);

    /// <summary>Days recorded with typed times instead of the usual clock.</summary>
    public int ChangedDays =>
        Tiles.Count(tile =>
            tile.Day.Registration?.Intent == WorkRegistrationIntent.ManualRegistration);

    /// <summary>One sentence a clerk can read about gaps this week.</summary>
    public string GapLabel => GapSentence(UnrecordedWorkingDays);

    /// <summary>One sentence about days that were not the usual clock.</summary>
    public string ChangedLabel => ChangedSentence(ChangedDays);

    /// <summary>Gap copy shared with month close.</summary>
    public static string GapSentence(int unrecorded) =>
        unrecorded == 0
            ? "Every working day is recorded."
            : unrecorded == 1
                ? "1 working day is not recorded."
                : $"{unrecorded} working days are not recorded.";

    /// <summary>Changed-day copy shared with month close.</summary>
    public static string ChangedSentence(int changed) =>
        changed == 0
            ? string.Empty
            : changed == 1
                ? "1 day was changed from the usual hours."
                : $"{changed} days were changed from the usual hours.";

    /// <summary>Monday on or before <paramref name="date"/>.</summary>
    public static DateOnly MondayOnOrBefore(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    /// <summary>Status chip for one day tile, including attendance-only workplaces.</summary>
    public static string ChipFor(WorkDayResponse day)
    {
        ArgumentNullException.ThrowIfNull(day);
        if (day.Tags.Any(tag => tag.Contains("PublicHoliday", StringComparison.OrdinalIgnoreCase)))
        {
            return "Holiday";
        }

        if (day.PaidEntitlement > TimeSpan.Zero && !day.IsWorkingDay)
        {
            return "Paid";
        }

        if (day.AppliedRules.Any(rule =>
            string.Equals(rule.LayerKind, "TemporaryOverride", StringComparison.Ordinal)))
        {
            return "Override";
        }

        if (!day.IsWorkingDay)
        {
            return "Closed";
        }

        if (day.Registration is null)
        {
            return "Not recorded";
        }

        return day.Registration.Intent == WorkRegistrationIntent.ManualRegistration
            ? "Changed"
            : "As planned";
    }

    /// <summary>Second line on a week row: clock and hours, or why there is nothing to do.</summary>
    public static string DetailFor(WorkDayResponse day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var chip = ChipFor(day);
        if (chip == "Holiday")
        {
            return "Public holiday";
        }

        if (chip == "Closed")
        {
            return "Shop shut";
        }

        if (chip == "Paid")
        {
            return "Paid day";
        }

        if (day.Registration is null)
        {
            return "Still to record";
        }

        var clock = RecordedClock(day);
        var hours = HoursClock.Format(day.ActualWorked > TimeSpan.Zero ? day.ActualWorked : day.ExpectedWork);
        return string.IsNullOrEmpty(clock) ? hours : $"{clock} · {hours}";
    }

    private static string RecordedClock(WorkDayResponse day)
    {
        if (day.WorkedIntervals.Length > 0)
        {
            return string.Join(
                " and ",
                day.WorkedIntervals.Select(interval =>
                    HoursClock.Format(
                        TimeOnly.FromTimeSpan(interval.Start.TimeOfDay),
                        TimeOnly.FromTimeSpan(interval.End.TimeOfDay))));
        }

        if (day.RoutineRanges.Length == 0)
        {
            return string.Empty;
        }

        return HoursClock.Format(day.RoutineRanges[0].Start, day.RoutineRanges[^1].End);
    }
}
