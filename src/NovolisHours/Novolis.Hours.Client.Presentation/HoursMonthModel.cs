using System.Collections.Immutable;
using System.Globalization;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Civil month made of ISO weeks, each week made of seven days.</summary>
public sealed class HoursMonthModel
{
    /// <summary>Builds a month from projected days. Missing dates become empty cells that still sit on their week.</summary>
    public HoursMonthModel(int year, int month, IReadOnlyList<WorkDayResponse> days, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        ArgumentNullException.ThrowIfNull(days);
        Year = year;
        Month = month;
        Today = today;
        First = new DateOnly(year, month, 1);
        Last = First.AddMonths(1).AddDays(-1);
        Title = First.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
        var byDate = days.ToDictionary(day => day.NominalDate);
        var firstMonday = WeekStudioModel.MondayOnOrBefore(First);
        var lastMonday = WeekStudioModel.MondayOnOrBefore(Last);
        var weeks = ImmutableArray.CreateBuilder<HoursMonthWeek>();
        for (var monday = firstMonday; monday <= lastMonday; monday = monday.AddDays(7))
        {
            var weekDays = ImmutableArray.CreateBuilder<HoursMonthDay>(7);
            for (var offset = 0; offset < 7; offset++)
            {
                var date = monday.AddDays(offset);
                byDate.TryGetValue(date, out var day);
                var chip = day is null
                    ? date.Month == month ? "Not recorded" : string.Empty
                    : WeekStudioModel.ChipFor(day);
                var detail = day is null ? string.Empty : WeekStudioModel.DetailFor(day);
                weekDays.Add(new HoursMonthDay(
                    date,
                    date.Month == month,
                    date == today,
                    HoursClock.FormatDate(date).Split(' ')[0],
                    chip,
                    detail,
                    day));
            }

            weeks.Add(new HoursMonthWeek(
                ISOWeek.GetYear(monday),
                ISOWeek.GetWeekOfYear(monday),
                monday,
                weekDays.ToImmutable()));
        }

        Weeks = weeks.ToImmutable();
    }

    /// <summary>Civil year.</summary>
    public int Year { get; }

    /// <summary>Civil month, 1-12.</summary>
    public int Month { get; }

    /// <summary>First day of the month.</summary>
    public DateOnly First { get; }

    /// <summary>Last day of the month.</summary>
    public DateOnly Last { get; }

    /// <summary>Reference today.</summary>
    public DateOnly Today { get; }

    /// <summary>Month title a clerk can read, for example October 2026.</summary>
    public string Title { get; }

    /// <summary>ISO weeks that touch this month, each with seven days.</summary>
    public ImmutableArray<HoursMonthWeek> Weeks { get; }

    /// <summary>Working days in this month with no registration.</summary>
    public int UnrecordedWorkingDays =>
        Weeks.SelectMany(week => week.Days)
            .Count(day => day.InMonth && day.Day is { IsWorkingDay: true, Registration: null });

    /// <summary>Days in this month recorded off the usual clock.</summary>
    public int ChangedDays =>
        Weeks.SelectMany(week => week.Days)
            .Count(day =>
                day.InMonth &&
                day.Day?.Registration?.Intent == WorkRegistrationIntent.ManualRegistration);

    /// <summary>Gap copy for the month.</summary>
    public string GapLabel => WeekStudioModel.GapSentence(UnrecordedWorkingDays);

    /// <summary>Changed-day copy for the month.</summary>
    public string ChangedLabel => WeekStudioModel.ChangedSentence(ChangedDays);
}
