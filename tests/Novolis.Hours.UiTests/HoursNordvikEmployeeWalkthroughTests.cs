using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

[NotInParallel("hours-ui")]
public sealed class HoursNordvikEmployeeWalkthroughTests : HoursUiTestBase
{
    [Test]
    public async Task Ada_records_flex_and_can_submit_then_dispute()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = WeekStudioModel.MondayOnOrBefore(today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        await SignInAsync("ada", SeedPassword);
        await Session.StepAsync("Nordvik week is Open with Flex", async page =>
        {
            await Expect(page.GetByText("Ada Lovelace")).ToBeVisibleAsync();
            await Expect(page.GetByText("Nordvik")).ToBeVisibleAsync();
            await Expect(page.GetByText("Europe/Oslo")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Flex")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Shop hours")).ToHaveCountAsync(0);
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Open" }).First)
                .ToBeVisibleAsync();
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Confirm" })).ToHaveCountAsync(0);
            await page.Locator(".week-tile").Filter(new() { HasText = "Open" }).First.ClickAsync();
        });
        await Session.StepAsync("Working day keeps paint", async page =>
        {
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Worked as scheduled" }))
                .ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToBeVisibleAsync();
            await Expect(page.Locator(".summary-grid").GetByText("Flex")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I was here" })).ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Button, new() { Name = "Worked as scheduled" }).ClickAsync();
            await Expect(page.GetByText("7:30").First).ToBeVisibleAsync();
        });

        var periodId = await CreateReviewPeriodAsync("ada", SeedPassword, monthStart, monthEnd);
        await Session.StepAsync("Ada submits and disputes", async page =>
        {
            await page.GotoAsync($"/reviews/{periodId:D}");
            await Expect(page.GetByText("cascading-approval")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dispute" })).ToBeVisibleAsync();
            await Expect(page.GetByText("manager-level-1")).ToBeVisibleAsync();
            await Expect(page.GetByText("hr-final")).ToBeVisibleAsync();
            await page.GetByLabel("Comment").FillAsync("October submitted.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
            await page.GetByLabel("Comment").FillAsync("Core hours were covered.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Dispute" }).ClickAsync();
            await Expect(page.GetByText("Dispute")).ToBeVisibleAsync();
        });
    }
}
