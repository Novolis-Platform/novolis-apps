using Novolis.Hours.Client.Presentation;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>One week of exceptions on seeded Game (jamie). Not a happy-path month.</summary>
public sealed class HoursWorkerDiscrepancyTests : HoursUiTestBase
{
    [Test]
    [Timeout(240_000)]
    public async Task Clerk_records_late_short_overtime_closed_and_a_forgotten_day(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var firstThursday = GameWorkingDays(monthStart, monthStart.AddMonths(1).AddDays(-1))
            .First(day => day.DayOfWeek == DayOfWeek.Thursday);
        var weekStart = WeekStudioModel.MondayOnOrBefore(firstThursday);
        var weekDays = GameWorkingDays(weekStart, weekStart.AddDays(6));
        var planned = weekDays.Where(day =>
                day.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday)
            .ToArray();
        var longDay = weekDays.First(day => day.DayOfWeek == DayOfWeek.Wednesday);
        var late = weekDays.First(day => day.DayOfWeek == DayOfWeek.Thursday);
        var forgotten = weekDays.First(day => day.DayOfWeek == DayOfWeek.Friday);
        var leftEarly = weekDays.First(day => day.DayOfWeek == DayOfWeek.Saturday);
        var sunday = weekStart.AddDays(6);

        Part("1. Usual hours");
        await SignInAsync(
            "jamie",
            SeedPassword,
            "Jamie is a Game clerk. First job is the usual clock, not a strip.");
        await SetUsualHoursAsync(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            "09:00 to 18:00 is how the shop usually runs.");

        Part("2. One week, five different outcomes");
        await ConfirmShopDaysAsync(planned, "Jamie:", recordedText: "9:00");
        await RecordChangedDayAsync(
            longDay,
            new TimeOnly(9, 0),
            new TimeOnly(20, 0),
            "11:00",
            "Changed",
            "Wednesday ran long. The row must show 09:00–20:00 and 11:00.");
        await RecordChangedDayAsync(
            late,
            new TimeOnly(10, 0),
            new TimeOnly(18, 0),
            "8:00",
            "Changed",
            "Late Thursday. Same length. The row must say Changed, not As planned.");
        await RecordChangedDayAsync(
            leftEarly,
            new TimeOnly(9, 0),
            new TimeOnly(16, 0),
            "7:00",
            "Changed",
            "Saturday cut short. Duration 7:00 moves the end to 16:00.",
            durationText: "7:00");
        await RecordClosedDayAsync(sunday, "Closed is not a forgotten day. The row says Shop shut.");
        using (Flow($"{HoursClock.FormatDate(forgotten)} · Not recorded", PlaywrightWalkthroughFlowKind.Deviated))
        {
            await Session.StepAsync(
                "Friday still not recorded",
                "Jamie never opened Friday. The week says one working day is not recorded.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, forgotten);
                    await Expect(page.Locator($"a.week-tile[href$='/{forgotten:yyyy-MM-dd}']"))
                        .ToContainTextAsync("Not recorded");
                    await Expect(page.Locator($"a.week-tile[href$='/{forgotten:yyyy-MM-dd}']"))
                        .ToContainTextAsync("Still to record");
                    await Expect(page.GetByText("1 working day is not recorded.")).ToBeVisibleAsync();
                    await Expect(page.GetByText("3 days were changed from the usual hours.")).ToBeVisibleAsync();
                });
        }
    }
}
