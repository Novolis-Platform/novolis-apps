using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Platform opens a customer in every catalog location except the UK Game month.</summary>
public sealed class HoursLocationCatalogTests : HoursUiTestBase
{
    [Test]
    [Timeout(300_000)]
    public async Task Platform_opens_norway_france_poland_and_finland(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        Part("1. Norway");
        await SignInAsync("system", SeedPassword, "Backoffice opens customers. Location first.");
        await OpenCustomerAsync(
            "Norway",
            "Norwegian public holidays.",
            "Fjord Office",
            $"fjord-{suffix}",
            "Sigrid Admin",
            $"sigrid-{suffix}");
        await SignOutAsync("Norway is open.");
        await SignInAsync($"sigrid-{suffix}", SetupPassword, "Sigrid is the Norwegian administrator. No week.");
        await Session.StepAsync(
            "Workplace, not a week",
            "Customer admin home is Workplace. Rules and People.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Workplace" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Rules" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "This week" })).ToHaveCountAsync(0);
            });
        await SignOutAsync("Sigrid is done.");

        Part("2. France");
        await SignInAsync("system", SeedPassword, "Same backoffice. Next location is France.");
        await OpenCustomerAsync(
            "France",
            "French public holidays.",
            "Atelier Loire",
            $"loire-{suffix}",
            "Camille Admin",
            $"camille-{suffix}");
        await SignOutAsync("France is open.");

        Part("3. Poland");
        await SignInAsync("system", SeedPassword, "Poland next.");
        await OpenCustomerAsync(
            "Poland",
            "Polish public holidays.",
            "Warsaw Yard",
            $"yard-{suffix}",
            "Ola Admin",
            $"ola-{suffix}");
        await SignOutAsync("Poland is open.");

        Part("4. Finland");
        await SignInAsync("system", SeedPassword, "Finland next.");
        await OpenCustomerAsync(
            "Finland",
            "Finnish public holidays.",
            "Helsinki Dock",
            $"dock-{suffix}",
            "Aino Admin",
            $"aino-{suffix}");
        await SignOutAsync("Finland is open.");

        Part("5. United States");
        await SignInAsync("system", SeedPassword, "FLSA shop. Forty hours, then overtime.");
        await OpenCustomerAsync(
            "United States",
            "US federal holidays.",
            "Bay Warehouse",
            $"bay-{suffix}",
            "Reese Admin",
            $"reese-{suffix}");
        await SignOutAsync("United States is open.");

        Part("6. Canada");
        await SignInAsync("system", SeedPassword, "Ontario ESA. Overtime after 44 hours.");
        await OpenCustomerAsync(
            "Canada",
            "Canadian public holidays.",
            "Harbour Yard",
            $"harbour-{suffix}",
            "Quinn Admin",
            $"quinn-ca-{suffix}");
        await SignOutAsync("Canada is open.");

        Part("7. Japan");
        await SignInAsync("system", SeedPassword, "Article 36 overtime sits on the rules, not a painted strip.");
        await OpenCustomerAsync(
            "Japan",
            "Japanese national holidays.",
            "Shibuya Flex",
            $"shibuya-{suffix}",
            "Haru Admin",
            $"haru-{suffix}");
        await SignOutAsync("Japan is open.");

        Part("8. Germany");
        await SignInAsync("system", SeedPassword, "Eight-hour day and an 11-hour rest.");
        await OpenCustomerAsync(
            "Germany",
            "German public holidays.",
            "Mitte Office",
            $"mitte-{suffix}",
            "Greta Admin",
            $"greta-{suffix}");
        await SignOutAsync("Every catalog location can be opened with location, name, and an administrator.");
    }
}
