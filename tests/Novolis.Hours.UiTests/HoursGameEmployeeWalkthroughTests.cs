using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

[NotInParallel("hours-ui")]
public sealed class HoursGameEmployeeWalkthroughTests : HoursUiTestBase
{
    [Test]
    public async Task Jamie_confirms_shop_hours_and_Priya_is_the_only_approver()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = WeekStudioModel.MondayOnOrBefore(today);
        var sunday = weekStart.AddDays(6);

        await SignInAsync("jamie", SeedPassword);
        await Session.StepAsync("Game week is attendance chips", async page =>
        {
            await Expect(page.GetByText("Jamie Shaw")).ToBeVisibleAsync();
            await Expect(page.GetByText("Game")).ToBeVisibleAsync();
            await Expect(page.GetByText("Europe/London")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Shop hours")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Flex")).ToHaveCountAsync(0);
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Confirm" }).First)
                .ToBeVisibleAsync();
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Weekend" }))
                .ToBeVisibleAsync();
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Open" })).ToHaveCountAsync(0);
        });
        await Session.StepAsync("Working day is I was here", async page =>
        {
            await page.Locator(".week-tile").Filter(new() { HasText = "Confirm" }).First.ClickAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I was here" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Shop hours").First).ToBeVisibleAsync();
            await Expect(page.GetByText("Attendance")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Worked as scheduled" }))
                .ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Button, new() { Name = "I was here" }).ClickAsync();
        });
        await Session.StepAsync("Sunday and Christmas are closed", async page =>
        {
            await OpenDayAsync("jamie", sunday);
            await Expect(page.GetByText("Closed")).ToBeVisibleAsync();
            await OpenDayAsync("jamie", new DateOnly(2026, 12, 25));
            await Expect(page.GetByText("Closed")).ToBeVisibleAsync();
        });

        var periodId = await CreateReviewPeriodAsync("jamie", SeedPassword, weekStart, sunday);
        await Session.StepAsync("Jamie confirms attendance with no dispute", async page =>
        {
            await page.GotoAsync($"/reviews/{periodId:D}");
            await Expect(page.GetByText("attendance-hr")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Confirm attendance" }))
                .ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dispute" })).ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit" })).ToHaveCountAsync(0);
            await Expect(page.GetByText("employee-confirm")).ToBeVisibleAsync();
            await Expect(page.GetByText("hr-attendance")).ToBeVisibleAsync();
            await page.GetByLabel("Comment").FillAsync("I was here.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Confirm attendance" }).ClickAsync();
            await Expect(page.GetByText("complete").First).ToBeVisibleAsync();
        });

        await SignOutAsync();
        await SignInAsync("priya", SeedPassword);
        await Session.StepAsync("Priya HR closes the shop week", async page =>
        {
            await page.GotoAsync($"/reviews/{periodId:D}");
            await Expect(page.GetByText("Priya Shah")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Resolve" })).ToHaveCountAsync(0);
            await page.GetByLabel("Comment").FillAsync("HR attendance close.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
            await Expect(page.GetByText("Approved")).ToBeVisibleAsync();
        });
    }
}
