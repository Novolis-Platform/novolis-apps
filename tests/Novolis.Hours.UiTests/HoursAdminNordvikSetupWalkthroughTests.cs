using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

[NotInParallel("hours-ui")]
public sealed class HoursAdminNordvikSetupWalkthroughTests : HoursUiTestBase
{
    [Test]
    public async Task Administrator_creates_a_Nordvik_office_and_the_employee_keeps_flex()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workplaceId = $"fjord-{suffix}";
        var employeeId = $"sigrid-{suffix}";

        await SignInAsync("admin", "admin");
        await Session.StepAsync("Open Nordvik office pattern", async page =>
        {
            await page.GetByRole(AriaRole.Link, new() { Name = "Setup" }).ClickAsync();
            var office = page.GetByRole(AriaRole.Button, new() { Name = "Nordvik office" });
            await Expect(office).ToContainTextAsync("Norwegian flex");
            await office.ClickAsync();
            await Expect(page.GetByText("Add an employee and a manager. Flex and paint stay on."))
                .ToBeVisibleAsync();
            await Expect(page.GetByText("The day commit will say Worked as scheduled.")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        });
        await Session.StepAsync("Name Fjord Office and add Sigrid", async page =>
        {
            await page.GetByLabel("Workplace id").FillAsync(workplaceId);
            await page.GetByLabel("Display name").FillAsync("Fjord Office");
            await page.GetByRole(AriaRole.Button, new() { Name = "Create workplace" }).ClickAsync();
            await page.GetByLabel("Employee id").FillAsync(employeeId);
            await page.GetByLabel("Login").FillAsync(employeeId);
            await page.GetByLabel("Display name").FillAsync("Sigrid");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add person" }).ClickAsync();
            await Expect(page.GetByText("Sigrid · Employee")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Finish" }).ClickAsync();
            await Expect(page.GetByText("Worked as scheduled", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("I was here", new() { Exact = true })).ToHaveCountAsync(0);
        });

        await SignOutAsync();
        await SignInAsync(employeeId, SetupPassword);
        await Session.StepAsync("Sigrid week is Open with Flex", async page =>
        {
            await Expect(page.GetByText("Fjord Office")).ToBeVisibleAsync();
            await Expect(page.GetByText("Europe/Oslo")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Flex")).ToBeVisibleAsync();
            await Expect(page.Locator("header").GetByText("Shop hours")).ToHaveCountAsync(0);
            await page.Locator(".week-tile").Filter(new() { HasText = "Open" }).First.ClickAsync();
        });
        await Session.StepAsync("Sigrid day keeps paint", async page =>
        {
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Worked as scheduled" }))
                .ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Paint" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Flex").First).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I was here" })).ToHaveCountAsync(0);
        });
    }
}
