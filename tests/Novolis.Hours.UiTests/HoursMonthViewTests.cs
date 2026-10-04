using System.Globalization;
using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>The month is weeks with week numbers, and each week is seven days.</summary>
public sealed class HoursMonthViewTests : HoursUiTestBase
{
    [Test]
    [Timeout(180_000)]
    public async Task Clerk_opens_this_month_and_sees_iso_week_numbers(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = new DateOnly(today.Year, today.Month, 1);
        var title = HoursClock.FormatMonth(first);
        var firstMonday = WeekStudioModel.MondayOnOrBefore(first);
        var weekLabel = $"W{ISOWeek.GetWeekOfYear(firstMonday)}";

        await SignInAsync("jamie", SeedPassword, "Jamie needs the whole month, not only seven tiles.");
        await Session.StepAsync(
            "This month",
            $"{title} is weeks first. Each week has a number. Days sit on their week.",
            async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "This month" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = title })).ToBeVisibleAsync();
                await Expect(page.GetByText(weekLabel).First).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Grid, new() { Name = title })).ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Open a day from the month",
            "A month cell is a day. It opens the same day screen as a week tile.",
            async page =>
            {
                await page.Locator("a.month-day").First.ClickAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" })
                    .Or(page.GetByText("The shop is closed. Nothing to record.")))
                    .ToBeVisibleAsync();
            });
    }
}
