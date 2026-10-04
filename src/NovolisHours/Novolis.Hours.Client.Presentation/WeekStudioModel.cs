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
                Chip(day),
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

    /// <summary>Monday on or before <paramref name="date"/>.</summary>
    public static DateOnly MondayOnOrBefore(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static string Chip(WorkDayResponse day)
    {
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
            return "Weekend";
        }

        return day.Registration is null ? "Open" : "Recorded";
    }
}
