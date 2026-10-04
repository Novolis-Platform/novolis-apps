using Microsoft.Playwright;
using Novolis.Hours.Client;
using Novolis.Hours.Contracts;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Shared Hours API + Blazor host for TUnit.Playwright walkthroughs.</summary>
public abstract class HoursUiTestBase : PlaywrightTestBase
{
    /// <summary>Password for seeded acceptance identities (jamie, ada, priya).</summary>
    protected const string SeedPassword = "Acceptance-2026-Strong!";

    /// <summary>Password Setup.razor writes for people added during admin walkthroughs.</summary>
    protected const string SetupPassword = "Workplace-2026-Strong!";

    private static HoursUiHost? host;

    /// <summary>Started Hours API and Blazor origins for this class.</summary>
    protected static HoursUiHost Host =>
        host ?? throw new InvalidOperationException("The Hours UI host has not started.");

    [Before(Class)]
    public static async Task StartHostAsync() => host = await HoursUiHost.StartAsync();

    [After(Class)]
    public static async Task StopHostAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
            host = null;
        }
    }

    /// <inheritdoc />
    protected override PlaywrightSessionOptions CreateOptions() => new()
    {
        BaseUrl = host?.ClientUri,
        ArtifactDirectory = PlaywrightArtifactStore.ForCurrentTest(),
        WalkthroughTitle = TestContext.Current?.Metadata.TestName ?? "Hours UI",
    };

    /// <summary>Signs in through the door after pointing the WASM session at the in-process API.</summary>
    protected async Task SignInAsync(string login, string password)
    {
        await Session.StepAsync($"Sign in as {login}", async page =>
        {
            page.SetDefaultTimeout(90_000);
            await page.AddInitScriptAsync(
                $"localStorage.setItem('hours-service-url', '{Host.ApiUri.AbsoluteUri}');");
            await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(page.GetByText("Sign in")).ToBeVisibleAsync();
            await page.GetByLabel("Login").FillAsync(login);
            await page.GetByLabel("Password").FillAsync(password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Enter this week" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { NameRegex = new("–") }))
                .ToBeVisibleAsync();
        });
    }

    /// <summary>Returns to the week and signs out.</summary>
    protected async Task SignOutAsync()
    {
        await Session.StepAsync("Sign out", async page =>
        {
            await page.GetByRole(AriaRole.Link, new() { Name = "Week" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
            await Expect(page.GetByText("Sign in")).ToBeVisibleAsync();
        });
    }

    /// <summary>Opens one WorkDay route.</summary>
    protected async Task OpenDayAsync(string employeeId, DateOnly date) =>
        await Session.Page.GotoAsync($"/workdays/{Uri.EscapeDataString(employeeId)}/{date:yyyy-MM-dd}");

    /// <summary>Creates a review period through the API so the browser can open <c>/reviews/{id}</c>.</summary>
    protected async Task<Guid> CreateReviewPeriodAsync(string employeeId, string password, DateOnly from, DateOnly through)
    {
        using var client = HoursApiClient.Connect(Host.ApiUri);
        await client.SignInAsync(employeeId, password);
        return await client.CreateReviewPeriodAsync(new CreateReviewPeriodRequest(employeeId, from, through));
    }
}
