using Novolis.Hours.Contracts;

namespace Novolis.Hours.FeatureTests;

public sealed class AdminCustomerSetupFeatureTests
{
    [Test]
    public async Task Administrator_creates_a_Game_shop_whose_clerk_confirms_attendance()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var admin = await fixture.ConnectAdministratorAsync();
        var templates = await admin.ListWorkplaceTemplatesAsync();
        await Assert.That(templates.Select(item => item.Id)).Contains("game-retail");
        await Assert.That(templates.Single(item => item.Id == "game-retail").Prototype.AttendanceConfirmationOnly)
            .IsTrue();

        var workplace = await admin.CreateCustomerAsync(new CreateCustomerRequest(
            "game-high-street",
            "Game High Street",
            "game-retail"));
        await Assert.That(workplace.AttendanceConfirmationOnly).IsTrue();
        await Assert.That(workplace.AllowsFlex).IsFalse();
        await Assert.That(workplace.AllowsDispute).IsFalse();
        await Assert.That(workplace.SaturdayIsWorkingDay).IsTrue();
        await Assert.That(workplace.ReviewPolicyId).IsEqualTo("attendance-hr");

        await admin.CreateUserAsync(new CreateUserRequest(
            "clerk",
            "clerk",
            "Workplace-2026-Strong!",
            "Shop Clerk",
            HoursClientRole.Employee,
            workplace.Id,
            "division-uk",
            "team-high-street"));
        await admin.CreateUserAsync(new CreateUserRequest(
            "shop-hr",
            "shop-hr",
            "Workplace-2026-Strong!",
            "Shop HR",
            HoursClientRole.HumanResources,
            workplace.Id,
            "division-uk"));

        using var clerk = await fixture.ConnectAsync("clerk", "Workplace-2026-Strong!");
        var thursday = await clerk.GetWorkDayAsync("clerk", new DateOnly(2026, 10, 1));
        await Assert.That(thursday.OrganisationName).IsEqualTo("Game High Street");
        await Assert.That(thursday.AttendanceConfirmationOnly).IsTrue();
        await Assert.That(thursday.AllowsFlex).IsFalse();
        await Assert.That(thursday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        var saturday = await clerk.GetWorkDayAsync("clerk", new DateOnly(2026, 10, 3));
        await Assert.That(saturday.IsWorkingDay).IsTrue();
        var sunday = await clerk.GetWorkDayAsync("clerk", new DateOnly(2026, 10, 4));
        await Assert.That(sunday.IsWorkingDay).IsFalse();
    }

    [Test]
    public async Task Administrator_creates_a_Nordvik_office_whose_employee_keeps_flex()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var admin = await fixture.ConnectAdministratorAsync();
        var workplace = await admin.CreateCustomerAsync(new CreateCustomerRequest(
            "fjord-office",
            "Fjord Office",
            "nordvik-office"));
        await Assert.That(workplace.AllowsFlex).IsTrue();
        await Assert.That(workplace.AttendanceConfirmationOnly).IsFalse();
        await Assert.That(workplace.ReviewPolicyId).IsEqualTo("cascading-approval");

        await admin.CreateUserAsync(new CreateUserRequest(
            "sigrid",
            "sigrid",
            "Workplace-2026-Strong!",
            "Sigrid",
            HoursClientRole.Employee,
            workplace.Id,
            "division-north",
            "team-a"));

        using var employee = await fixture.ConnectAsync("sigrid", "Workplace-2026-Strong!");
        var thursday = await employee.GetWorkDayAsync("sigrid", new DateOnly(2026, 10, 1));
        await Assert.That(thursday.OrganisationName).IsEqualTo("Fjord Office");
        await Assert.That(thursday.AllowsFlex).IsTrue();
        await Assert.That(thursday.AttendanceConfirmationOnly).IsFalse();
        await Assert.That(thursday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        var saturday = await employee.GetWorkDayAsync("sigrid", new DateOnly(2026, 10, 3));
        await Assert.That(saturday.IsWorkingDay).IsFalse();
    }

    [Test]
    public async Task Seeded_Game_and_Nordvik_identities_survive_admin_setup()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var admin = await fixture.ConnectAdministratorAsync();
        await admin.CreateCustomerAsync(new CreateCustomerRequest(
            "extra-shop",
            "Extra Shop",
            "game-retail"));
        using var jamie = await fixture.ConnectAsync("jamie");
        var jamieDay = await jamie.GetWorkDayAsync("jamie", new DateOnly(2026, 10, 1));
        await Assert.That(jamieDay.AttendanceConfirmationOnly).IsTrue();
        using var ada = await fixture.ConnectAsync("ada");
        var adaDay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 10, 1));
        await Assert.That(adaDay.AllowsFlex).IsTrue();
    }

    [Test]
    public async Task System_opens_a_customer_from_location_and_admin_sets_saturday()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var system = await fixture.ConnectAsync("system");
        var locations = await system.ListLocationsAsync();
        await Assert.That(locations.Select(item => item.Id)).Contains("united-kingdom");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var organisationId = $"game-loc-{suffix}";
        var customer = await system.CreateCustomerAsync(new CreateCustomerRequest(
            organisationId,
            "Game High Street",
            TemplateId: null,
            LocationId: "united-kingdom"));
        await Assert.That(customer.CountryCode).IsEqualTo("GB");
        await Assert.That(customer.TimeZoneId).IsEqualTo("Europe/London");
        await Assert.That(customer.SaturdayIsWorkingDay).IsFalse();
        await Assert.That(customer.AllowsFlex).IsFalse();
        await system.CreateUserAsync(new CreateUserRequest(
            $"pat-{suffix}",
            $"pat-{suffix}",
            "Workplace-2026-Strong!",
            "Pat Shop Admin",
            HoursClientRole.Administrator,
            customer.Id,
            "division-uk",
            "team-high-street"));
        using var pat = await fixture.ConnectAsync($"pat-{suffix}", "Workplace-2026-Strong!");
        var saved = await pat.UpdateCustomerRulesAsync(
            customer.Id,
            new UpdateCustomerRulesRequest(true, false, false, "attendance-hr"));
        await Assert.That(saved.SaturdayIsWorkingDay).IsTrue();
        await pat.CreateUserAsync(new CreateUserRequest(
            $"robin-{suffix}",
            $"robin-{suffix}",
            "Workplace-2026-Strong!",
            "Robin Clerk",
            HoursClientRole.Employee,
            customer.Id,
            "division-uk",
            "team-high-street"));
        using var robin = await fixture.ConnectAsync($"robin-{suffix}", "Workplace-2026-Strong!");
        var saturday = await robin.GetWorkDayAsync($"robin-{suffix}", new DateOnly(2026, 10, 3));
        await Assert.That(saturday.IsWorkingDay).IsTrue();
        await robin.UpdateUsualHoursAsync(
            $"robin-{suffix}",
            new UpdateUsualHoursRequest(new TimeOnly(10, 0), new TimeOnly(18, 0)));
        var afterUsual = await robin.GetWorkDayAsync($"robin-{suffix}", new DateOnly(2026, 10, 1));
        await Assert.That(afterUsual.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(afterUsual.RoutineRanges.Single().Start).IsEqualTo(new TimeOnly(10, 0));
        await Assert.That(afterUsual.RoutineRanges.Single().End).IsEqualTo(new TimeOnly(18, 0));
    }

    [Test]
    public async Task System_provisions_a_Game_shop()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var system = await fixture.ConnectAsync("system");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workplace = await system.CreateCustomerAsync(new CreateCustomerRequest(
            $"game-sys-{suffix}",
            "Game System Shop",
            "game-retail"));
        await Assert.That(workplace.AttendanceConfirmationOnly).IsTrue();
        await Assert.That(workplace.ReviewPolicyId).IsEqualTo("attendance-hr");
        await system.CreateUserAsync(new CreateUserRequest(
            $"pat-{suffix}",
            $"pat-{suffix}",
            "Workplace-2026-Strong!",
            "Pat Shop Admin",
            HoursClientRole.Administrator,
            workplace.Id,
            "division-uk",
            "team-high-street"));
        using var pat = await fixture.ConnectAsync($"pat-{suffix}", "Workplace-2026-Strong!");
        await pat.CreateUserAsync(new CreateUserRequest(
            $"robin-{suffix}",
            $"robin-{suffix}",
            "Workplace-2026-Strong!",
            "Robin Clerk",
            HoursClientRole.Employee,
            workplace.Id,
            "division-uk",
            "team-high-street"));
        using var robin = await fixture.ConnectAsync($"robin-{suffix}", "Workplace-2026-Strong!");
        var thursday = await robin.GetWorkDayAsync($"robin-{suffix}", new DateOnly(2026, 10, 1));
        await Assert.That(thursday.AttendanceConfirmationOnly).IsTrue();
    }

    [Test]
    public async Task Employee_cannot_create_a_workplace()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        using var ada = await fixture.ConnectAsync("ada");
        var thrown = await Assert.That(async () =>
                await ada.CreateCustomerAsync(new CreateCustomerRequest(
                    "rogue-shop",
                    "Rogue",
                    "game-retail")))
            .Throws<HttpRequestException>();
        await Assert.That(thrown!.Message).Contains("403");
    }
}
