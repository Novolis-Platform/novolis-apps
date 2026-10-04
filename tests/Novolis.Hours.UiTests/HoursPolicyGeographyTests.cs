using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>US, Canadian, Japanese, and German clerks record against their statutory stories.</summary>
public sealed class HoursPolicyGeographyTests : HoursUiTestBase
{
    [Test]
    [Timeout(180_000)]
    public async Task Jordan_records_a_California_week(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("jordan", SeedPassword, "Jordan is Pacific Yard. Eight hours. No flex strip.");
        await Session.StepAsync(
            "Pacific Yard",
            "Workplace is Pacific Yard. Saturday is shut unless the admin opens it.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Pacific Yard")).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Jordan:", recordedText: "8:00");
        await RecordChangedDayAsync(
            monday.AddDays(1),
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            "10:00",
            "Changed",
            "Tuesday ran long. California would call the last two hours daily overtime.");
        await RecordClosedDayAsync(monday.AddDays(6), "Sunday is shut at the yard.");
    }

    [Test]
    [Timeout(180_000)]
    public async Task Casey_records_an_Ontario_week(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("casey", SeedPassword, "Casey is Toronto Yard. Overtime after 44 hours, not 40.");
        await Session.StepAsync(
            "Toronto Yard",
            "Workplace is Toronto Yard.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Toronto Yard")).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Casey:", recordedText: "8:00");
        await RecordChangedDayAsync(
            monday.AddDays(2),
            new TimeOnly(8, 0),
            new TimeOnly(14, 0),
            "6:00",
            "Changed",
            "Wednesday was short. Ontario does not pay daily overtime for that.");
    }

    [Test]
    [Timeout(180_000)]
    public async Task Yuki_records_a_Tokyo_flex_day(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("yuki", SeedPassword, "Yuki is Tokyo Flex. Article 36 is a rule, not a chip.");
        await Session.StepAsync(
            "Tokyo week",
            "Workplace is Tokyo Flex. Flex and a day strip live here.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Tokyo Flex")).ToBeVisibleAsync();
            });
        using (Flow($"{HoursClock.FormatDate(monday)} · flex"))
        {
            await Session.StepAsync(
                "Monday has a strip",
                "Japanese flextime still starts with I worked as planned.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, monday);
                    await page.Locator($"a.week-tile[href$='/{monday:yyyy-MM-dd}']").ClickAsync();
                    await page.GetByText("Day strip").ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToBeVisibleAsync();
                    await page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }).ClickAsync();
                    await Expect(page.Locator(".summary-grid div")
                            .Filter(new() { HasText = "Recorded" })
                            .Locator("strong"))
                        .ToHaveTextAsync("8:00");
                    await ReturnToWeekAsync(page);
                });
        }
    }

    [Test]
    [Timeout(180_000)]
    public async Task Lena_records_a_Berlin_day(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("lena", SeedPassword, "Lena is Berlin Office. Eight hours, then rest.");
        await Session.StepAsync(
            "Berlin week",
            "Workplace is Berlin Office. No flex strip.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Berlin Office")).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Lena:", recordedText: "8:00");
        await RecordClosedDayAsync(monday.AddDays(6), "German Sunday is shut.");
    }
}