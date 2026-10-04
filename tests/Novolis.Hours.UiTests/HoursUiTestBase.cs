using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using Novolis.Testing.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

/// <summary>Shared Hours API + Blazor host for TUnit.Playwright scenario recordings.</summary>
[NotInParallel("hours-ui")]
public abstract class HoursUiTestBase : PlaywrightTestBase
{
    /// <summary>Password for seeded acceptance identities (system, jamie, ada).</summary>
    protected const string SeedPassword = "Acceptance-2026-Strong!";

    /// <summary>Password Setup and People write for new people.</summary>
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
        WalkthroughTitle = TestContext.Current?.Metadata.TestName ?? "Hours scenario",
        FrameHoldMilliseconds = 4000,
    };

    /// <summary>Starts a recording part.</summary>
    protected void Part(string name) => Session.BeginPart(name);

    /// <summary>Starts a nested recording flow. Dispose to leave it.</summary>
    protected PlaywrightWalkthroughFlow Flow(
        string name,
        PlaywrightWalkthroughFlowKind kind = PlaywrightWalkthroughFlowKind.Default) =>
        Session.BeginFlow(name, kind);

    /// <summary>Signs in through the door after pointing the WASM session at the in-process API.</summary>
    protected async Task SignInAsync(string login, string password, string narration)
    {
        await Session.StepAsync(
            $"Sign in as {login}",
            narration,
            async page =>
            {
                page.SetDefaultTimeout(90_000);
                await page.AddInitScriptAsync(
                    $"localStorage.setItem('hours-service-url', '{Host.ApiUri.AbsoluteUri}');");
                await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
                    .ToBeVisibleAsync();
                await page.GetByText("Host settings").ClickAsync();
                await page.GetByLabel("Hours service URL").FillAsync(Host.ApiUri.AbsoluteUri);
                await page.GetByLabel("Login").FillAsync(login);
                await page.GetByLabel("Password").FillAsync(password);
                await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
                var signOut = page.GetByRole(AriaRole.Button, new() { Name = "Sign out" });
                try
                {
                    await Expect(signOut).ToBeVisibleAsync(new() { Timeout = 15_000 });
                }
                catch (PlaywrightException)
                {
                    await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
                    await Expect(signOut).ToBeVisibleAsync();
                }
            });
    }

    /// <summary>Returns to the week and signs out.</summary>
    protected async Task SignOutAsync(string narration)
    {
        await Session.StepAsync(
            "Sign out",
            narration,
            async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }))
                    .ToBeVisibleAsync();
            });
    }

    /// <summary>Monday-first Game shop working days (Sunday closed).</summary>
    protected static IReadOnlyList<DateOnly> GameWorkingDays(DateOnly from, DateOnly through)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= through; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Sunday)
            {
                days.Add(date);
            }
        }

        return days;
    }

    /// <summary>Records a shop month with optional late and short days in date order.</summary>
    protected async Task RecordWorkerMonthAsync(
        IReadOnlyList<DateOnly> days,
        string narrationPrefix,
        DateOnly lateDay,
        DateOnly shortDay,
        string plannedText = "8:00")
    {
        foreach (var date in days)
        {
            if (date == lateDay)
            {
                await RecordChangedDayAsync(
                    date,
                    new TimeOnly(10, 0),
                    new TimeOnly(18, 0),
                    "8:00",
                    "Changed",
                    $"{narrationPrefix} Late start 10:00–18:00. Same length, different clock.");
                continue;
            }

            if (date == shortDay)
            {
                await RecordChangedDayAsync(
                    date,
                    new TimeOnly(9, 0),
                    new TimeOnly(16, 0),
                    "7:00",
                    "Changed",
                    $"{narrationPrefix} Left at 16:00. Short day. HR will see Changed.");
                continue;
            }

            await ConfirmShopDaysAsync([date], narrationPrefix, plannedText);
        }
    }

    /// <summary>Opens each working-day tile and records I worked as planned.</summary>
    protected async Task ConfirmShopDaysAsync(
        IReadOnlyList<DateOnly> days,
        string narrationPrefix,
        string recordedText = "8:00")
    {
        foreach (var date in days)
        {
            await Session.StepAsync(
                $"{HoursClock.FormatDate(date)} · as planned",
                $"{narrationPrefix} {HoursClock.FormatDate(date)} follows the usual hours. I worked as planned.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, date);
                    var tile = page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']");
                    await tile.ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                        .ToBeVisibleAsync();
                    await page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }).ClickAsync();
                    await Expect(page.Locator(".summary-grid div")
                            .Filter(new() { HasText = "Recorded" })
                            .Locator("strong"))
                        .ToHaveTextAsync(recordedText);
                    await ReturnToWeekAsync(page);
                    await EnsureWeekContainsAsync(page, date);
                    await Expect(page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']"))
                        .ToContainTextAsync("As planned");
                });
        }
    }

    /// <summary>Opens My hours, shows the form, saves the clock, and returns to the week.</summary>
    protected async Task SetUsualHoursAsync(TimeOnly start, TimeOnly end, string narration)
    {
        using (Flow("Set usual hours"))
        {
            await Session.StepAsync(
                "Open My hours",
                "Usual hours is a start and an end. It is not a strip.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                        .GetByRole(AriaRole.Link, new() { Name = "My hours" })
                        .ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Usual hours" }))
                        .ToBeVisibleAsync();
                    await Expect(page.GetByLabel("Usual start")).ToBeVisibleAsync();
                    await Expect(page.GetByLabel("Usual end")).ToBeVisibleAsync();
                });
            await Session.StepAsync(
                $"Enter {HoursClock.Format(start, end)}",
                narration,
                async page =>
                {
                    await page.GetByLabel("Usual start").FillAsync(start.ToString("HH:mm"));
                    await page.GetByLabel("Usual end").FillAsync(end.ToString("HH:mm"));
                    await Expect(page.GetByLabel("Usual start")).ToHaveValueAsync(start.ToString("HH:mm"));
                    await Expect(page.GetByLabel("Usual end")).ToHaveValueAsync(end.ToString("HH:mm"));
                });
            await Session.StepAsync(
                "Usual hours saved",
                "I worked as planned will use this clock.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Button, new() { Name = "Save usual hours" }).ClickAsync();
                    await Expect(page.GetByText("Usual hours saved")).ToBeVisibleAsync();
                });
            await Session.StepAsync(
                "Back on this week",
                "The clerk is done with the usual clock.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                        .GetByRole(AriaRole.Link, new() { Name = "This week" })
                        .ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                        .GetByRole(AriaRole.Link, new() { Name = "This week" }))
                        .ToBeVisibleAsync();
                });
        }
    }

    /// <summary>Records a day that was not the usual clock, with a frame for each screen.</summary>
    protected async Task RecordChangedDayAsync(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string actualText,
        string chip,
        string narration,
        string? durationText = null)
    {
        var dayName = HoursClock.FormatDate(date);
        using (Flow($"{dayName} · Changed", PlaywrightWalkthroughFlowKind.Deviated))
        {
            await Session.StepAsync(
                "Open the day",
                $"{dayName} still shows the usual hours. This flow is about to leave them.",
                async page =>
                {
                    await EnsureWeekContainsAsync(page, date);
                    await page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']").ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                        .ToBeVisibleAsync();
                    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "I worked different hours" }))
                        .ToBeVisibleAsync();
                });
            using (Flow("I worked different hours", PlaywrightWalkthroughFlowKind.Deviated))
            {
                await Session.StepAsync(
                    durationText is null
                        ? $"Type {HoursClock.Format(start, end)}"
                        : $"Type start {HoursClock.Format(start)} and duration {durationText}",
                    narration,
                    async page =>
                    {
                        await page.GetByLabel("Start").FillAsync(start.ToString("HH:mm"));
                        if (durationText is null)
                        {
                            await page.GetByLabel("End").FillAsync(end.ToString("HH:mm"));
                        }
                        else
                        {
                            await page.GetByLabel("Duration").FillAsync(durationText);
                        }

                        await Expect(page.GetByLabel("Start")).ToHaveValueAsync(start.ToString("HH:mm"));
                    });
                await Session.StepAsync(
                    $"You recorded {HoursClock.Format(start, end)} ({actualText})",
                    "The day must show the typed clock before the clerk leaves.",
                    async page =>
                    {
                        await page.GetByRole(AriaRole.Button, new() { Name = "Save these hours" }).ClickAsync();
                        await Expect(page.Locator(".summary-grid div")
                                .Filter(new() { HasText = "Recorded" })
                                .Locator("strong"))
                            .ToHaveTextAsync(actualText);
                        await Expect(page.GetByText($"You recorded {HoursClock.Format(start, end)} ({actualText})."))
                            .ToBeVisibleAsync();
                    });
            }

            await Session.StepAsync(
                $"Week row says {chip} · {HoursClock.Format(start, end)}",
                "Changed is the first word. The clock is the second.",
                async page =>
                {
                    await ReturnToWeekAsync(page);
                    await EnsureWeekContainsAsync(page, date);
                    var tile = page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']");
                    await Expect(tile).ToContainTextAsync(chip);
                    await Expect(tile).ToContainTextAsync(start.ToString("HH:mm"));
                    await Expect(tile).ToContainTextAsync(actualText);
                });
        }
    }

    /// <summary>Opens a shut day, shows there is no record button, and returns to Closed on the week.</summary>
    protected async Task RecordClosedDayAsync(DateOnly date, string narration)
    {
        var dayName = HoursClock.FormatDate(date);
        using (Flow($"{dayName} · Closed", PlaywrightWalkthroughFlowKind.Closed))
        {
            await Session.StepAsync(
                "The shop is closed",
                narration,
                async page =>
                {
                    await EnsureWeekContainsAsync(page, date);
                    await page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']").ClickAsync();
                    await Expect(page.GetByText("The shop is closed. Nothing to record.")).ToBeVisibleAsync();
                    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "I worked as planned" }))
                        .ToHaveCountAsync(0);
                });
            await Session.StepAsync(
                "Week row says Shop shut",
                "Closed is not a forgotten day.",
                async page =>
                {
                    await ReturnToWeekAsync(page);
                    await Expect(page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']"))
                        .ToContainTextAsync("Shop shut");
                });
        }
    }

    /// <summary>Opens a customer from Backoffice Setup. Caller is already signed in as system.</summary>
    protected async Task OpenCustomerAsync(
        string locationName,
        string calendarHint,
        string displayName,
        string organisationId,
        string adminName,
        string adminLogin)
    {
        using (Flow($"Open {displayName}"))
        {
            await Session.StepAsync(
                "Open Setup",
                "Location, name, and the customer administrator. No week.",
                async page =>
                {
                    await page.GetByRole(AriaRole.Link, new() { Name = "Setup" }).ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Open a customer" }))
                        .ToBeVisibleAsync();
                });
            await Session.StepAsync(
                $"Choose {locationName}",
                calendarHint,
                async page =>
                {
                    var location = page.GetByRole(AriaRole.Button, new() { Name = locationName });
                    await Expect(location).ToContainTextAsync(calendarHint.Split('.')[0]);
                    await location.ClickAsync();
                });
            await Session.StepAsync(
                $"Name {displayName}",
                "The platform names the customer and hands it to an administrator.",
                async page =>
                {
                    await page.GetByLabel("Customer name").FillAsync(displayName);
                    await page.GetByLabel("Organisation id").FillAsync(organisationId);
                    await page.GetByLabel("Administrator name").FillAsync(adminName);
                    await page.GetByLabel("Login").FillAsync(adminLogin);
                    await page.GetByLabel("Employee id").FillAsync(adminLogin);
                    await page.GetByRole(AriaRole.Button, new() { Name = "Open customer" }).ClickAsync();
                    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = displayName }))
                        .ToBeVisibleAsync();
                    await Expect(page.GetByText(adminName)).ToBeVisibleAsync();
                });
        }
    }

    /// <summary>Asserts this role has no timesheet chrome.</summary>
    protected async Task ExpectNoWeekChromeAsync(string heading)
    {
        await Session.StepAsync(
            $"{heading} has no week",
            "The role owns the home. A timesheet is not the default.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "This week" })).ToHaveCountAsync(0);
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Today" })).ToHaveCountAsync(0);
            });
    }

    /// <summary>First Monday on or before today.</summary>
    protected static DateOnly ThisMonday() =>
        WeekStudioModel.MondayOnOrBefore(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Returns to this week from a day.</summary>
    protected static Task ReturnToWeekAsync(IPage page) =>
        page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
            .GetByRole(AriaRole.Link, new() { Name = "This week" })
            .ClickAsync();

    /// <summary>Walks Previous/Next until the week heading is the Monday that contains the day.</summary>
    protected async Task EnsureWeekContainsAsync(IPage page, DateOnly date)
    {
        var targetMonday = WeekStudioModel.MondayOnOrBefore(date);
        var mondayLabel = HoursClock.FormatDate(targetMonday);
        var heading = page.GetByRole(AriaRole.Heading, new() { Level = 1 });
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var text = await heading.InnerTextAsync();
            if (text.Contains(mondayLabel, StringComparison.Ordinal))
            {
                await Expect(page.Locator($"a.week-tile[href$='/{date:yyyy-MM-dd}']")).ToBeVisibleAsync();
                return;
            }

            var firstHref = await page.Locator("a.week-tile").First.GetAttributeAsync("href");
            var shown = firstHref is not null && DateOnly.TryParse(firstHref.Split('/')[^1], out var parsed)
                ? parsed
                : targetMonday;
            if (shown < targetMonday)
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
            }
            else
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Previous" }).ClickAsync();
            }

            await Expect(heading).Not.ToHaveTextAsync(text);
        }

        throw new TimeoutException($"The week grid never showed {date:yyyy-MM-dd}.");
    }
}
