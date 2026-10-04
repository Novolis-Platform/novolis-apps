using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Seeded clerks in FR, PL, FI, and the 7-day Norwegian station.</summary>
public sealed class HoursCountryClerkTests : HoursUiTestBase
{
    [Test]
    [Timeout(180_000)]
    public async Task Pierre_records_a_Paris_week_with_saturday_closed(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        var saturday = monday.AddDays(5);
        await SignInAsync("pierre", SeedPassword, "Pierre is Atelier Curie. Paris, 7:00 days, Saturday shut.");
        await Session.StepAsync(
            "Atelier Curie week",
            "Workplace is Atelier Curie. Saturday is not a shop day.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Atelier Curie")).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Pierre:", recordedText: "7:00");
        await RecordChangedDayAsync(
            monday.AddDays(1),
            new TimeOnly(10, 0),
            new TimeOnly(16, 0),
            "6:00",
            "Changed",
            "Pierre typed a short Tuesday.");
        await RecordClosedDayAsync(saturday, "French Saturday is closed. Not a gap.");
    }

    [Test]
    [Timeout(180_000)]
    public async Task Anna_records_a_Warsaw_week(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("anna", SeedPassword, "Anna is Warsaw Settlement. 8:00 days. Employer closes the month.");
        await Session.StepAsync(
            "Warsaw week",
            "Workplace is Warsaw Settlement.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Warsaw Settlement")).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Anna:", recordedText: "8:00");
        await RecordChangedDayAsync(
            monday.AddDays(2),
            new TimeOnly(8, 0),
            new TimeOnly(14, 0),
            "6:00",
            "Changed",
            "Anna recorded a short Wednesday.");
        await RecordClosedDayAsync(monday.AddDays(6), "Sunday is shut in Warsaw.");
    }

    [Test]
    [Timeout(180_000)]
    public async Task Liisa_records_a_Helsinki_flex_day(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync("liisa", SeedPassword, "Liisa is Helsinki Flex. 7:30 days and a strip.");
        await Session.StepAsync(
            "Helsinki week",
            "Workplace is Helsinki Flex. Flex is on.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Helsinki Flex")).ToBeVisibleAsync();
            });
        using (Flow($"{HoursClock.FormatDate(monday)} · flex", PlaywrightWalkthroughFlowKind.Planned))
        {
            await Session.StepAsync(
                "Monday has a strip",
                "Helsinki keeps flex. Paint lives under Day strip.",
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
                        .ToHaveTextAsync("7:30");
                    await ReturnToWeekAsync(page);
                });
        }

        await RecordChangedDayAsync(
            monday.AddDays(3),
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            "10:00",
            "Changed",
            "Liisa ran long on Thursday.");
    }

    [Test]
    [Timeout(180_000)]
    public async Task Bob_records_a_station_sunday(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sunday = ThisMonday().AddDays(6);
        await SignInAsync("bob", SeedPassword, "Bob is Nordvik Station. Seven-day operation. Sunday is work.");
        await Session.StepAsync(
            "Station week",
            "Workplace is Nordvik Station. Sunday is not Shop shut.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Nordvik Station")).ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Sunday is a working day",
            "Station Sunday has I worked as planned. Game Sunday does not.",
            async page =>
            {
                await EnsureWeekContainsAsync(page, sunday);
                await page.Locator($"a.week-tile[href$='/{sunday:yyyy-MM-dd}']").ClickAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                    .ToBeVisibleAsync();
                await Expect(page.GetByText("The shop is closed. Nothing to record.")).ToHaveCountAsync(0);
                await page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }).ClickAsync();
                await Expect(page.Locator(".summary-grid div")
                        .Filter(new() { HasText = "Recorded" })
                        .Locator("strong"))
                    .ToHaveTextAsync("7:30");
                await ReturnToWeekAsync(page);
                await Expect(page.Locator($"a.week-tile[href$='/{sunday:yyyy-MM-dd}']"))
                    .ToContainTextAsync("As planned");
            });
    }
}
