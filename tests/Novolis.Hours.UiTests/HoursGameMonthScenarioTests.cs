using Microsoft.Playwright;
using Novolis.Hours.Client.Presentation;
using TUnit.Core;
using TUnit.Playwright;

namespace Novolis.Hours.UiTests;

[NotInParallel("hours-ui")]
public sealed class HoursGameMonthScenarioTests : HoursUiTestBase
{
    [Test]
    [Timeout(600_000)]
    public async Task Game_shop_from_platform_customer_through_month_hr_and_auditor(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workplaceId = $"game-hs-{suffix}";
        var adminId = $"pat-{suffix}";
        var robinId = $"robin-{suffix}";
        var samId = $"sam-{suffix}";
        var hrId = $"morgan-{suffix}";
        var auditorId = $"quinn-{suffix}";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var workingDays = GameWorkingDays(monthStart, monthEnd);
        var lateDay = workingDays.First(day => day.DayOfWeek == DayOfWeek.Thursday);
        var shortDay = workingDays.First(day => day.DayOfWeek == DayOfWeek.Saturday);
        var samSkip = workingDays.First(day => day.DayOfWeek == DayOfWeek.Friday && day.Day >= 10);
        var sunday = monthStart;
        while (sunday.DayOfWeek != DayOfWeek.Sunday)
        {
            sunday = sunday.AddDays(1);
        }

        Part("1. Platform adds the customer");
        await SignInAsync(
            "system",
            SeedPassword,
            "The platform system user lands in Backoffice. There is no week.");
        await Session.StepAsync(
            "Backoffice, not a week",
            "System chrome is Backoffice and Setup. No Week, Today, Flex, or shop hours.",
            async page =>
            {
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Backoffice" }))
                    .ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Week" })).ToHaveCountAsync(0);
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Today" })).ToHaveCountAsync(0);
            });
        await Session.StepAsync(
            "Open Setup",
            "Only the platform opens a customer. Location, name, and the customer administrator.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Setup" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Open a customer" }))
                    .ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Choose United Kingdom",
            "Location sets English holidays and Europe/London. The week defaults to Monday to Friday.",
            async page =>
            {
                var unitedKingdom = page.GetByRole(AriaRole.Button, new() { Name = "United Kingdom" });
                await Expect(unitedKingdom).ToContainTextAsync("English public holidays");
                await unitedKingdom.ClickAsync();
            });
        await Session.StepAsync(
            "Name the customer and the administrator",
            "The platform names the customer and hands it to Pat. Seeded Game (jamie/priya) stays untouched.",
            async page =>
            {
                await page.GetByLabel("Customer name").FillAsync("Game High Street");
                await page.GetByLabel("Organisation id").FillAsync(workplaceId);
                await page.GetByLabel("Administrator name").FillAsync("Pat Shop Admin");
                await page.GetByLabel("Login").FillAsync(adminId);
                await page.GetByLabel("Employee id").FillAsync(adminId);
                await page.GetByRole(AriaRole.Button, new() { Name = "Open customer" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Game High Street" }))
                    .ToBeVisibleAsync();
                await Expect(page.GetByText("Pat Shop Admin")).ToBeVisibleAsync();
            });
        await SignOutAsync("Platform work is done. The customer administrator now owns the rules.");

        Part("2. Customer sets up the shop");
        await SignInAsync(
            adminId,
            SetupPassword,
            "Pat is Game High Street's administrator. Rules first, then People.");
        await Session.StepAsync(
            "Set Saturday as a working day",
            "The United Kingdom location starts Monday to Friday. Game High Street trades Saturday.",
            async page =>
            {
                await page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                    .GetByRole(AriaRole.Link, new() { Name = "Rules" })
                    .ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Rules" })).ToBeVisibleAsync();
                await page.GetByLabel("Saturday is a working day").CheckAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Save rules" }).ClickAsync();
                await Expect(page.GetByText("Rules saved")).ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Open People",
            "Employees record Worked as planned. HR closes the month. An auditor can read the trail and cannot approve.",
            async page =>
            {
                await page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                    .GetByRole(AriaRole.Link, new() { Name = "People" })
                    .ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "People" })).ToBeVisibleAsync();
            });
        await AddRosterPersonAsync(robinId, "Robin Clerk", "Employee", "First clerk. Most days as planned, one late start, one short Saturday.");
        await AddRosterPersonAsync(samId, "Sam Clerk", "Employee", "Second clerk. Same month, one Friday left unrecorded.");
        await AddRosterPersonAsync(hrId, "Morgan HR", "HR", "Only HR approves. There is no store-manager step.");
        await AddRosterPersonAsync(auditorId, "Quinn Auditor", "Auditor", "Auditor reads facts. They do not stamp Approve.");
        await SignOutAsync("The shop exists. The clerks can now record October.");

        Part("3. Robin registers the month");
        await SignInAsync(
            robinId,
            SetupPassword,
            "Robin sees Game High Street and Not recorded chips, never Flex or Paint.");
        await SetUsualHoursAsync(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            "Robin sets the clock the shop usually runs. After that, most days are I worked as planned.");
        await Session.StepAsync(
            "Robin's week is the usual schedule",
            "Game High Street. Saturday is a working day. Sunday is closed. Empty tiles say Not recorded.",
            async page =>
            {
                await Expect(page.Locator("header").GetByText("Game High Street")).ToBeVisibleAsync();
                await Expect(page.Locator("header").GetByText("Flex")).ToHaveCountAsync(0);
                await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hours navigation" })
                    .GetByRole(AriaRole.Link, new() { Name = "This week" })).ToBeVisibleAsync();
            });
        await RecordWorkerMonthAsync(workingDays, "Robin:", lateDay, shortDay, plannedText: "9:00");
        Guid robinReview = default;
        await Session.StepAsync(
            "Robin hands October to HR",
            "Every shop day is recorded. Two days were changed. That is the month HR must read.",
            async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                await page.WaitForURLAsync("**/reviews/**");
                robinReview = Guid.Parse(new Uri(page.Url).Segments[^1].TrimEnd('/'));
                await Expect(page.GetByText("Every working day is recorded.")).ToBeVisibleAsync();
                await Expect(page.GetByText("2 days were changed from the usual hours.")).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dispute" })).ToHaveCountAsync(0);
                await page.GetByLabel("Comment").FillAsync("Late Thursday and a short Saturday. The rest as planned.");
                await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                await Expect(page.GetByText("Done").First).ToBeVisibleAsync();
            });
        await SignOutAsync("Robin has submitted the month. Sam still has a gap.");

        Part("4. Sam registers the month with a gap");
        await SignInAsync(
            samId,
            SetupPassword,
            "Sam works the same usual hours. One Friday is left unrecorded on purpose.");
        await RecordClosedDayAsync(
            sunday,
            "Game does not open Sunday. The day says Closed. That is not a missed record.");
        await ConfirmShopDaysAsync(workingDays.Where(day => day != samSkip).ToArray(), "Sam:");
        await Session.StepAsync(
            "Friday left unrecorded",
            $"{HoursClock.FormatDate(samSkip)} still says Not recorded. That is the gap HR must see.",
            async page =>
            {
                await EnsureWeekContainsAsync(page, samSkip);
                await Expect(page.Locator($"a.week-tile[href*='{samSkip:yyyy-MM-dd}']"))
                    .ToContainTextAsync("Not recorded");
            });
        Guid samReview = default;
        await Session.StepAsync(
            "Sam hands October to HR",
            "Sam hands the month over with one Friday missing. The gap stays on the period.",
            async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                await page.WaitForURLAsync("**/reviews/**");
                samReview = Guid.Parse(new Uri(page.Url).Segments[^1].TrimEnd('/'));
                await Expect(page.GetByText("1 working day is not recorded.")).ToBeVisibleAsync();
                await page.GetByLabel("Comment").FillAsync("I missed one Friday. The rest as planned.");
                await page.GetByRole(AriaRole.Button, new() { Name = "Hand month to HR" }).ClickAsync();
                await Expect(page.GetByText("Done").First).ToBeVisibleAsync();
            });
        await SignOutAsync("Both clerks have handed the month to HR.");

        Part("5. HR closes both periods");
        await SignInAsync(
            hrId,
            SetupPassword,
            "Morgan is HR. Reviews is the inbox. Approve is the only close. There is no manager ladder and no Resolve.");
        await ApproveFromInboxAsync(robinId, robinReview, "Robin changed two days. HR still closes the month.");
        await ApproveFromInboxAsync(samId, samReview, "Sam left one Friday unrecorded. HR still closes the month.");
        await SignOutAsync("HR has approved. The auditor may read, not rewrite.");

        Part("6. Auditor reads the trail");
        await SignInAsync(
            auditorId,
            SetupPassword,
            "Quinn is the shop auditor. Audit is in the chrome. They cannot Approve.");
        await Session.StepAsync(
            "Open Robin's audit trail",
            "Every confirm, review period, and HR approve is an append-only fact. Nothing is edited in place.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Audit" }).ClickAsync();
                await page.GetByLabel("Employee id").FillAsync(robinId);
                await page.GetByRole(AriaRole.Button, new() { Name = "Open audit trail" }).ClickAsync();
                await Expect(page.GetByText("Append-only facts")).ToBeVisibleAsync();
                await Expect(page.GetByText("review.period-created.v1").First).ToBeVisibleAsync();
            });
        await Session.StepAsync(
            "Auditor has no Approve",
            "Opening Robin's review as auditor shows the history. Approve is not offered.",
            async page =>
            {
                await page.GotoAsync($"/reviews/{robinReview:D}");
                await Expect(page.GetByText("Approved")).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToHaveCountAsync(0);
            });
    }

    private async Task AddRosterPersonAsync(string id, string displayName, string role, string narration)
    {
        await Session.StepAsync(
            $"Add {displayName}",
            narration,
            async page =>
            {
                await page.GetByLabel("Employee id").FillAsync(id);
                await page.GetByLabel("Login").FillAsync(id);
                await page.GetByLabel("Display name").FillAsync(displayName);
                await page.GetByLabel("Role").SelectOptionAsync(new SelectOptionValue { Label = role });
                await page.GetByRole(AriaRole.Button, new() { Name = "Add person" }).ClickAsync();
                await Expect(page.GetByText($"{displayName} ·")).ToBeVisibleAsync();
            });
    }

    private async Task ApproveFromInboxAsync(string employeeId, Guid periodId, string comment)
    {
        await Session.StepAsync(
            $"Open {employeeId} from Reviews",
            "HR does not type a period id. The inbox lists every month opened on this shop.",
            async page =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "Reviews" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Reviews" })).ToBeVisibleAsync();
                await page.GetByRole(AriaRole.Link, new() { NameRegex = new(employeeId) }).ClickAsync();
                await page.WaitForURLAsync($"**/reviews/{periodId:D}");
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Resolve" })).ToHaveCountAsync(0);
            });
        await Session.StepAsync(
            $"Approve {employeeId}",
            comment,
            async page =>
            {
                await page.GetByLabel("Comment").FillAsync(comment);
                await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
                await Expect(page.GetByText("Approved")).ToBeVisibleAsync();
            });
    }
}
