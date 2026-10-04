using Novolis.Hours.Client.Presentation;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Game holidays, and surfaces a clerk must never see.</summary>
public sealed class HoursHolidayAndForbiddenTests : HoursUiTestBase
{
    [Test]
    [Timeout(240_000)]
    public async Task Game_holidays_and_forbidden_clerk_surfaces(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var christmas = new DateOnly(2026, 12, 25);
        var boxing = new DateOnly(2026, 12, 28);

        Part("1. Clerk cannot open a customer");
        await SignInAsync("jamie", SeedPassword, "Jamie is a clerk. Setup is platform only.");
        await Session.StepAsync(
            "Setup is blocked",
            "A clerk who types /setup sees Platform only.",
            async page =>
            {
                await page.GotoAsync("/setup");
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Platform only" }))
                    .ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Open customer" }))
                    .ToHaveCountAsync(0);
            });

        Part("2. No paint, no dispute, no flex");
        var monday = ThisMonday();
        using (Flow("Game day forbids flex chrome", PlaywrightWalkthroughFlowKind.Planned))
        {
            await Session.StepAsync(
                "Open a working day",
                "I worked as planned. No Day strip. No Paint. No Flex.",
                async page =>
                {
                    await page.GotoAsync("/");
                    await EnsureWeekContainsAsync(page, monday);
                    if (!monday.DayOfWeek.Equals(DayOfWeek.Sunday))
                    {
                        await page.Locator($"a.week-tile[href$='/{monday:yyyy-MM-dd}']").ClickAsync();
                        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                            .ToBeVisibleAsync();
                        await Expect(page.GetByText("Day strip")).ToHaveCountAsync(0);
                        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" }))
                            .ToHaveCountAsync(0);
                        await Expect(page.Locator(".summary-grid").GetByText("Flex")).ToHaveCountAsync(0);
                    }
                });
        }

        Part("3. Christmas and Boxing Day");
        using (Flow($"{HoursClock.FormatDate(christmas)} · Holiday", PlaywrightWalkthroughFlowKind.Closed))
        {
            await Session.StepAsync(
                "Christmas is a holiday",
                "25 December 2026 is shut. Holiday, not Not recorded.",
                async page =>
                {
                    await page.GotoAsync("/");
                    await EnsureWeekContainsAsync(page, christmas);
                    await Expect(page.Locator($"a.week-tile[href$='/{christmas:yyyy-MM-dd}']"))
                        .ToContainTextAsync("Holiday");
                    await page.Locator($"a.week-tile[href$='/{christmas:yyyy-MM-dd}']").ClickAsync();
                    await Expect(page.GetByText("The shop is closed. Nothing to record.")).ToBeVisibleAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                        .ToHaveCountAsync(0);
                    await ReturnToWeekAsync(page);
                });
        }

        using (Flow($"{HoursClock.FormatDate(boxing)} · Holiday", PlaywrightWalkthroughFlowKind.Closed))
        {
            await Session.StepAsync(
                "Boxing Day observed",
                "28 December 2026 is the observed Boxing Day. Still shut.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, boxing);
                    var tile = page.Locator($"a.week-tile[href$='/{boxing:yyyy-MM-dd}']");
                    await Expect(tile).ToContainTextAsync("Holiday");
                });
        }

        Part("4. No dispute on the month");
        await Session.StepAsync(
            "Hand month has no Dispute",
            "Game attendance-hr has no dispute.",
            async page =>
            {
                await page.GotoAsync("/");
                await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                await page.WaitForURLAsync("**/reviews/**");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dispute" })).ToHaveCountAsync(0);
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }))
                    .ToBeVisibleAsync();
            });
    }
}
