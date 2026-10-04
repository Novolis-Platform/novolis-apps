using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

[NotInParallel("hours-ui")]
public sealed class HoursAdminGameSetupWalkthroughTests : HoursUiTestBase
{
    [Test]
    public async Task Administrator_creates_a_Game_shop_and_the_clerk_only_confirms_attendance()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workplaceId = $"game-hs-{suffix}";
        var clerkId = $"clerk-{suffix}";
        var hrId = $"shophr-{suffix}";

        await SignInAsync("admin", "admin");
        await Session.StepAsync("Open Game shop pattern", async page =>
        {
            await page.GetByRole(AriaRole.Link, new() { Name = "Setup" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Choose a pattern" }))
                .ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Nordvik office" }))
                .ToBeVisibleAsync();
            var game = page.GetByRole(AriaRole.Button, new() { Name = "Game shop" });
            await Expect(game).ToContainTextAsync("No flex, no overtime, no dispute");
            await game.ClickAsync();
            await Expect(page.GetByText("Add a shop clerk and HR. There is no store-manager approve step."))
                .ToBeVisibleAsync();
            await Expect(page.GetByText("The day commit will say I was here.")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        });
        await Session.StepAsync("Name Game High Street", async page =>
        {
            await page.GetByLabel("Workplace id").FillAsync(workplaceId);
            await page.GetByLabel("Display name").FillAsync("Game High Street");
            await page.GetByRole(AriaRole.Button, new() { Name = "Create workplace" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Add people" })).ToBeVisibleAsync();
        });
        await Session.StepAsync("Add clerk and HR", async page =>
        {
            await AddPersonAsync(page, clerkId, "Shop Clerk", "Employee");
            await Expect(page.GetByText("Shop Clerk · Employee")).ToBeVisibleAsync();
            await AddPersonAsync(page, hrId, "Shop HR", "HR");
            await Expect(page.GetByText("Shop HR · HR")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Finish" }).ClickAsync();
            await Expect(page.GetByText("People confirm time with")).ToBeVisibleAsync();
            await Expect(page.GetByText("I was here", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Worked as scheduled", new() { Exact = true })).ToHaveCountAsync(0);
        });

        await SignOutAsync();
        await SignInAsync(clerkId, SetupPassword);
        await Session.StepAsync("Clerk week is Confirm, not Open", async page =>
        {
            await Expect(page.GetByText("Game High Street")).ToBeVisibleAsync();
            await Expect(page.GetByText("Europe/London")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Shop hours")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Flex")).ToHaveCountAsync(0);
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Confirm" }).First)
                .ToBeVisibleAsync();
            await Expect(page.Locator(".week-tile").Filter(new() { HasText = "Open" })).ToHaveCountAsync(0);
            await page.Locator(".week-tile").Filter(new() { HasText = "Confirm" }).First.ClickAsync();
        });
        await Session.StepAsync("Clerk day is attendance only", async page =>
        {
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I was here" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Shop hours").First).ToBeVisibleAsync();
            await Expect(page.GetByText("Attendance")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Worked as scheduled" }))
                .ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Button, new() { Name = "I was here" }).ClickAsync();
            await Expect(page.GetByText("8:00").First).ToBeVisibleAsync();
        });
        await Session.StepAsync("Setup is administrator only", async page =>
        {
            await page.GotoAsync("/setup");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Administrator only" }))
                .ToBeVisibleAsync();
        });
    }

    private static async Task AddPersonAsync(IPage page, string id, string displayName, string role)
    {
        await page.GetByLabel("Employee id").FillAsync(id);
        await page.GetByLabel("Login").FillAsync(id);
        await page.GetByLabel("Display name").FillAsync(displayName);
        await page.GetByLabel("Role").SelectOptionAsync(new SelectOptionValue { Label = role });
        await page.GetByRole(AriaRole.Button, new() { Name = "Add person" }).ClickAsync();
    }
}
