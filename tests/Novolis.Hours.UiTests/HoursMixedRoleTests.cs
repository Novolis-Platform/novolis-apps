using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>HR who is also admin, and a manager who is also employee and admin.</summary>
public sealed class HoursMixedRoleTests : HoursUiTestBase
{
    [Test]
    [Timeout(240_000)]
    public async Task Alice_records_her_own_week_then_opens_workplace_and_reviews(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        if (monday.DayOfWeek == DayOfWeek.Sunday)
        {
            monday = monday.AddDays(1);
        }

        Part("1. Working manager");
        await SignInAsync(
            "alice",
            SeedPassword,
            "Alice approves Ada, records her own hours, and can open Workplace.");
        await ConfirmShopDaysAsync([monday], "Alice:", recordedText: "7:30");
        await Session.StepAsync(
            "Workplace is still hers",
            "A working manager does not lose People and Rules.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Workplace" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Workplace" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "People" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Rules" })).ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Reviews stay in the chrome",
            "The same person can approve after she has recorded her own Monday.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Reviews" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Reviews" })).ToBeVisibleAsync();
            });
    }

    [Test]
    [Timeout(180_000)]
    public async Task Helen_is_hr_and_admin_without_a_week(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SignInAsync("helen", SeedPassword, "Helen closes months and sets the rules.");
        await ExpectNoWeekChromeAsync("Workplace");
        await Session.StepAsync(
            "Rules name the Norwegian week",
            "37.5 hours. Surplus is flex. That sentence belongs on Rules, not a timesheet.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Rules" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Rules" })).ToBeVisibleAsync();
                await Expect(page.GetByText("37.5-hour office week")).ToBeVisibleAsync();
            });
    }

    [Test]
    [Timeout(180_000)]
    public async Task Pat_is_a_US_working_store_lead(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monday = ThisMonday();
        await SignInAsync(
            "pat",
            SeedPassword,
            "Pat is manager, employee, and admin on Pacific Yard.");
        await Session.StepAsync(
            "Pacific Yard week",
            "Pat has a week. Workplace and Reviews stay in the chrome.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Pacific Yard")).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "This week" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Workplace" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Reviews" })).ToBeVisibleAsync();
            });
        await ConfirmShopDaysAsync([monday], "Pat:", recordedText: "8:00");
        await Session.StepAsync(
            "FLSA sentence on Rules",
            "Forty hours, then overtime. California also has a daily line.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Rules" }).ClickAsync();
                await Expect(page.GetByText("FLSA: time-and-a-half after 40 hours")).ToBeVisibleAsync();
            });
    }
}
