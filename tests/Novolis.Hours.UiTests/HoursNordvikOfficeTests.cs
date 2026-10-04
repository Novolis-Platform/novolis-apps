using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Nordvik flex: paint, a changed day, submit, dispute, manager, HR, auditor.</summary>
public sealed class HoursNordvikOfficeTests : HoursUiTestBase
{
    [Test]
    [Timeout(300_000)]
    public async Task Ada_records_flex_change_submits_and_disputes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        var tuesday = monday.AddDays(1);
        var saturday = monday.AddDays(5);

        Part("1. Flex day");
        await SignInAsync("ada", SeedPassword, "Ada keeps flex. The day has a strip and Paint.");
        using (Flow($"{HoursClock.FormatDate(monday)} · as planned", PlaywrightWalkthroughFlowKind.Planned))
        {
            await Session.StepAsync(
                "Open Monday",
                "Usual Nordvik clock. Flex and a day strip live here. Game clerks never see this.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, monday);
                    await page.Locator($"a.week-tile[href$='/{monday:yyyy-MM-dd}']").ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                        .ToBeVisibleAsync();
                    await page.GetByText("Day strip").ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToBeVisibleAsync();
                    await Expect(page.GetByText("Flex")).ToBeVisibleAsync();
                });
            await Session.StepAsync(
                "I worked as planned",
                "Usual Nordvik day is 7:30.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }).ClickAsync();
                    await Expect(page.Locator(".summary-grid div")
                            .Filter(new() { HasText = "Recorded" })
                            .Locator("strong"))
                        .ToHaveTextAsync("7:30");
                    await ReturnToWeekAsync(page);
                    await Expect(page.Locator($"a.week-tile[href$='/{monday:yyyy-MM-dd}']"))
                        .ToContainTextAsync("As planned");
                });
        }

        Part("2. Changed Tuesday");
        await RecordChangedDayAsync(
            tuesday,
            new TimeOnly(9, 0),
            new TimeOnly(15, 0),
            "6:00",
            "Changed",
            "Ada left early. Flex must show a short day as Changed.");

        Part("3. Saturday is shut");
        await RecordClosedDayAsync(saturday, "Nordvik Saturday is a weekend. Not a forgotten day.");

        Part("4. Hand to HR and dispute");
        Guid period = default;
        using (Flow("Hand month to HR", PlaywrightWalkthroughFlowKind.Deviated))
        {
            await Session.StepAsync(
                "Open the month",
                "Nordvik can dispute. Game cannot.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                    await page.WaitForURLAsync("**/reviews/**");
                    period = Guid.Parse(new Uri(page.Url).Segments[^1].TrimEnd('/'));
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dispute" })).ToBeVisibleAsync();
                    await page.GetByLabel("Comment").FillAsync("Tuesday was short. The rest as planned.");
                    await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                    await Expect(page.GetByText("Done").First).ToBeVisibleAsync();
                });
            await Session.StepAsync(
                "Dispute",
                "Dispute is a real Nordvik action. The month is not silently closed.",
                async page =>
                {
                    await page.GetByLabel("Comment").FillAsync("Core hours were covered.");
                    await page.GetByRole(AriaRole.Button, new() { Name = "Dispute" }).ClickAsync();
                    await Expect(page.GetByText("Dispute")).ToBeVisibleAsync();
                });
        }

        await SignOutAsync("Ada has submitted and disputed. Managers and HR still have work.");

        Part("5. Manager, HR, auditor");
        await SignInAsync("alice", SeedPassword, "Alice is level-1 manager. She can Approve.");
        await Session.StepAsync(
            "Alice opens Ada's month",
            "Manager Approve is on the cascading ladder.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Reviews" }).ClickAsync();
                await page.GotoAsync($"/reviews/{period:D}");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeVisibleAsync();
            });
        await SignOutAsync("Alice saw Approve. HR still closes.");
        await SignInAsync("helen", SeedPassword, "Helen is HR. Resolve is available after a dispute.");
        await Session.StepAsync(
            "Helen can Resolve",
            "HR resolves a dispute. The auditor still cannot Approve.",
            async page =>
            {
                await page.GotoAsync($"/reviews/{period:D}");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Resolve" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeVisibleAsync();
            });
        await SignOutAsync("HR is done looking.");
        await SignInAsync("audrey", SeedPassword, "Audrey reads. She does not stamp.");
        await Session.StepAsync(
            "Auditor has no Approve",
            "Opening Ada's review as auditor shows history. Approve is not offered.",
            async page =>
            {
                await page.GotoAsync($"/reviews/{period:D}");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToHaveCountAsync(0);
            });
    }
}
