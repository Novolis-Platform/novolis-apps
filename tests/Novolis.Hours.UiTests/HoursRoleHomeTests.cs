using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Every seeded role lands on its own home. A timesheet is not the default.</summary>
public sealed class HoursRoleHomeTests : HoursUiTestBase
{
    [Test]
    [Timeout(240_000)]
    public async Task Every_seeded_role_lands_on_its_own_home(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Part("1. Platform");
        await SignInAsync("system", SeedPassword, "System is Backoffice. There is no week.");
        await ExpectNoWeekChromeAsync("Backoffice");
        await SignOutAsync("Platform leaves. The next person must not inherit a week.");

        Part("2. Game clerk");
        await SignInAsync("jamie", SeedPassword, "Jamie is a Game clerk. Home is this week.");
        await Session.StepAsync(
            "Game week",
            "This week, Today, My hours. No Flex.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                    .GetByRole(AriaRole.Link, new() { Name = "This week" })).ToBeVisibleAsync();
                await Expect(page.Locator("header").GetByText("Game")).ToBeVisibleAsync();
                await Expect(page.Locator("header").GetByText("Flex")).ToHaveCountAsync(0);
            });
        await SignOutAsync("Jamie is done.");

        Part("3. Nordvik office");
        await SignInAsync("ada", SeedPassword, "Ada is a Nordvik employee. Home is this week.");
        await Session.StepAsync(
            "Nordvik week",
            "Ada has a week. The workplace name is Nordvik.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                    .GetByRole(AriaRole.Link, new() { Name = "This week" })).ToBeVisibleAsync();
                await Expect(page.Locator("header").GetByText("Nordvik")).ToBeVisibleAsync();
            });
        await SignOutAsync("Ada is done.");

        Part("4. Mixed manager");
        await SignInAsync("alice", SeedPassword, "Alice is manager and employee. She keeps a week and still approves.");
        await Session.StepAsync(
            "Working manager chrome",
            "This week and Reviews. Pat on Pacific Yard is the store lead who is also admin.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "This week" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Reviews" })).ToBeVisibleAsync();
            });
        await SignOutAsync("Alice is done.");

        Part("5. HR who is also admin, then auditor");
        await SignInAsync("helen", SeedPassword, "Helen is HR and admin. Workplace is home. Reviews stays in the chrome.");
        await ExpectNoWeekChromeAsync("Workplace");
        await Session.StepAsync(
            "Helen has Reviews and Rules",
            "HR who is also admin does not lose the inbox.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Reviews" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Rules" })).ToBeVisibleAsync();
            });
        await SignOutAsync("Helen is done.");
        await SignInAsync("priya", SeedPassword, "Priya is Game HR and admin. Same mixed door.");
        await ExpectNoWeekChromeAsync("Workplace");
        await SignOutAsync("Priya is done.");
        await SignInAsync("audrey", SeedPassword, "Audrey is the auditor. Audit is the home.");
        await Session.StepAsync(
            "Audit home",
            "No week. No Approve in the chrome.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Audit" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "This week" })).ToHaveCountAsync(0);
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToHaveCountAsync(0);
            });
        await SignOutAsync("Every role had its own door.");
    }
}
