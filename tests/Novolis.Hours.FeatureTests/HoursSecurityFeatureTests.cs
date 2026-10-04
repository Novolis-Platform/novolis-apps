using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Hours.Server;
using Novolis.Security.Authentication;

namespace Novolis.Hours.FeatureTests;

public sealed class HoursSecurityFeatureTests
{
    [Test]
    public async Task Production_rejects_demo_credentials_before_the_host_can_start()
    {
        Func<Task> build = () =>
        {
            using var app = HoursServerApplication.Build(
                [],
                builder =>
                {
                    builder.WebHost.UseTestServer();
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Hours:EnableDemoAdminCredentials"] = "true",
                    });
                },
                environmentName: "Production");
            return Task.CompletedTask;
        };

        await Assert.That(build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task The_real_production_host_rejects_http_but_serves_https_and_sets_host_only_cookies()
    {
        await using var app = BuildProductionHost();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var httpResponse = await client.GetAsync("/api/auth/antiforgery");
        await Assert.That(httpResponse.StatusCode).IsEqualTo(HttpStatusCode.UpgradeRequired);

        var httpsResponse = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Get,
            "https://localhost/api/auth/antiforgery"));
        await Assert.That(httpsResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var antiforgeryCookie = SetCookie(httpsResponse, "__Host-NovolisHours.Antiforgery");
        var normalizedAntiforgeryCookie = antiforgeryCookie.ToLowerInvariant();
        await Assert.That(normalizedAntiforgeryCookie).Contains("secure");
        await Assert.That(normalizedAntiforgeryCookie).Contains("path=/");
        await Assert.That(normalizedAntiforgeryCookie).DoesNotContain("domain=");

        var token = ReadJsonString(
            await httpsResponse.Content.ReadAsStringAsync(),
            "token");
        var login = new HttpRequestMessage(
            HttpMethod.Post,
            "https://localhost/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                login = "admin",
                password = "Cobalt-Raven-45!",
            }),
        };
        login.Headers.Add("Cookie", CookiePair(antiforgeryCookie));
        login.Headers.Add("X-Novolis-Hours-CSRF", token);
        var loginResponse = await client.SendAsync(login);

        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var sessionCookie = SetCookie(loginResponse, "__Host-NovolisHours.Session");
        var normalizedSessionCookie = sessionCookie.ToLowerInvariant();
        await Assert.That(normalizedSessionCookie).Contains("httponly");
        await Assert.That(normalizedSessionCookie).Contains("secure");
        await Assert.That(normalizedSessionCookie).Contains("samesite=strict");
        await Assert.That(normalizedSessionCookie).Contains("path=/");
        await Assert.That(normalizedSessionCookie).DoesNotContain("domain=");
    }

    [Test]
    public async Task Production_refuses_to_start_while_the_legal_preset_is_unapproved()
    {
        Func<Task> build = () =>
        {
            using var app = HoursServerApplication.Build(
                [],
                builder =>
                {
                    builder.WebHost.UseTestServer();
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Hours:UseInMemoryJournal"] = "true",
                        ["Hours:EnableDemoAdminCredentials"] = "false",
                        ["Hours:InitialAdministratorPassword"] = "Cobalt-Raven-45!",
                    });
                },
                environmentName: "Production");
            return Task.CompletedTask;
        };

        await Assert.That(build).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_cross_site_browser_origin_receives_secure_none_cookies()
    {
        await using var app = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "false",
                    ["Hours:InitialAdministratorPassword"] = "Cobalt-Raven-45!",
                    ["Hours:RequireHttps"] = "true",
                    ["Hours:PermitDraftLegalPreset"] = "true",
                    ["Hours:AllowedClientOrigins:0"] = "https://hours.example",
                });
                builder.Services.RemoveAll<IPasswordBreachChecker>();
                builder.Services.AddSingleton<IPasswordBreachChecker>(
                    AllowingPasswordBreachChecker.Instance);
            },
            environmentName: "Production");
        await app.StartAsync();
        using var client = app.GetTestClient();

        var httpsResponse = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Get,
            "https://localhost/api/auth/antiforgery"));
        var antiforgeryCookie = SetCookie(httpsResponse, "__Host-NovolisHours.Antiforgery").ToLowerInvariant();
        await Assert.That(antiforgeryCookie).Contains("samesite=none");
        await Assert.That(antiforgeryCookie).Contains("secure");
        await Assert.That(antiforgeryCookie).DoesNotContain("domain=");
    }

    [Test]
    public async Task Login_is_rate_limited_without_replacing_security_lockout()
    {
        await using var app = BuildDevelopmentHost(
            ("Hours:EnableDemoAdminCredentials", "true"),
            ("Hours:RequireHttps", "false"));
        await app.StartAsync();
        var client = app.GetTestClient();
        var tokenResponse = await client.GetAsync("/api/auth/antiforgery");
        var token = ReadJsonString(await tokenResponse.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = SetCookie(tokenResponse, "NovolisHours.Antiforgery");

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    login = $"unknown-{attempt}",
                    password = "not-the-password",
                }),
            };
            request.Headers.Add("Cookie", CookiePair(antiforgeryCookie));
            request.Headers.Add("X-Novolis-Hours-CSRF", token);
            last = await client.SendAsync(request);
        }

        await Assert.That(last).IsNotNull();
        await Assert.That(last!.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task A_role_profile_change_is_reflected_by_the_existing_session()
    {
        await using var app = BuildDevelopmentHost(
            ("Hours:InitialAdministratorPassword", "Cobalt-Raven-45!"),
            ("Hours:RequireHttps", "false"));
        await app.StartAsync();
        var directory = app.Services.GetRequiredService<HoursUserDirectory>();
        var client = app.GetTestClient();
        var session = await SignInAsync(client, "admin", "Cobalt-Raven-45!");

        var administrator = directory.FindByEmployeeId("admin")
            ?? throw new InvalidOperationException("The bootstrap administrator was not created.");
        await directory.SaveAsync(administrator with { Role = HoursActorRole.Employee });

        using var current = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        current.Headers.Add("Cookie", session.Cookies);
        var response = await client.SendAsync(current);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(document.RootElement.GetProperty("role").GetString())
            .IsEqualTo(nameof(HoursActorRole.Employee));
    }

    [Test]
    public async Task Password_change_revokes_the_current_security_session()
    {
        await using var app = BuildDevelopmentHost(
            ("Hours:InitialAdministratorPassword", "Cobalt-Raven-45!"),
            ("Hours:RequireHttps", "false"));
        await app.StartAsync();
        var client = app.GetTestClient();
        var session = await SignInAsync(client, "admin", "Cobalt-Raven-45!");

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = "Cobalt-Raven-45!",
                newPassword = "Cobalt-Raven-46!",
            }),
        };
        change.Headers.Add("Cookie", session.Cookies);
        change.Headers.Add("X-Novolis-Hours-CSRF", session.AntiforgeryToken);
        var changeResponse = await client.SendAsync(change);
        await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var current = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        current.Headers.Add("Cookie", session.Cookies);
        await Assert.That((await client.SendAsync(current)).StatusCode)
            .IsEqualTo(HttpStatusCode.Unauthorized);

        var replacement = await SignInAsync(client, "admin", "Cobalt-Raven-46!");
        await Assert.That(replacement.Cookies).Contains("NovolisHours.Session=");
    }

    [Test]
    public async Task Antiforgery_and_authorization_failures_are_explicit_and_safe()
    {
        await using var app = BuildDevelopmentHost(
            ("Hours:InitialAdministratorPassword", "Cobalt-Raven-45!"),
            ("Hours:RequireHttps", "false"));
        await app.StartAsync();
        var directory = app.Services.GetRequiredService<HoursUserDirectory>();
        var client = app.GetTestClient();
        var session = await SignInAsync(client, "admin", "Cobalt-Raven-45!");

        using var missingToken = new HttpRequestMessage(HttpMethod.Post, "/api/work")
        {
            Content = JsonContent.Create(new
            {
                employeeId = "admin",
                day = "2026-10-01",
                startedAt = "08:00",
                endedAt = "16:00",
                comment = "Antiforgery boundary.",
                financialCompensationSlices = Array.Empty<object>(),
                managerAgreementRecorded = false,
            }),
        };
        missingToken.Headers.Add("Cookie", session.Cookies);
        var antiforgeryResponse = await client.SendAsync(missingToken);
        await Assert.That(antiforgeryResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var administrator = directory.FindByEmployeeId("admin")
            ?? throw new InvalidOperationException("The bootstrap administrator was not created.");
        await directory.SaveAsync(administrator with { Role = HoursActorRole.Employee });

        using var forbidden = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/employees/another-employee/view");
        forbidden.Headers.Add("Cookie", session.Cookies);
        var forbiddenResponse = await client.SendAsync(forbidden);
        await Assert.That(forbiddenResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Health_and_readiness_are_separate_anonymous_boundaries()
    {
        await using var app = BuildDevelopmentHost(
            ("Hours:EnableDemoAdminCredentials", "true"),
            ("Hours:RequireHttps", "false"));
        await app.StartAsync();
        var client = app.GetTestClient();

        await Assert.That((await client.GetAsync("/health/live")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.GetAsync("/health/ready")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.GetAsync("/health")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task An_employee_cannot_self_assert_manager_agreement()
    {
        var service = new HoursService(
            new InMemoryHoursJournal(),
            NorwegianHoursPolicy.Create(year: 2026));

        var entry = await service.RegisterWorkAsync(new RegisterWorkCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            new TimeOnly(9, 0),
            new TimeOnly(19, 0),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [new FinancialCompensationSlice(
                new TimeOnly(17, 0),
                new TimeOnly(19, 0),
                "Emergency")],
            "Employee cannot self-assert agreement.",
            ManagerAgreementRecorded: true,
            Actor: HoursActor.Employee("ada")));

        await Assert.That(entry.ManagerAgreementRecorded).IsFalse();
        await Assert.That(entry.LegalNotices.Any(item =>
                item.RuleId == "financial-compensation.manager-agreement"))
            .IsTrue();
    }

    private static WebApplication BuildProductionHost() =>
        HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "false",
                    ["Hours:InitialAdministratorPassword"] = "Cobalt-Raven-45!",
                    ["Hours:RequireHttps"] = "true",
                    ["Hours:PermitDraftLegalPreset"] = "true",
                });
                builder.Services.RemoveAll<IPasswordBreachChecker>();
                builder.Services.AddSingleton<IPasswordBreachChecker>(
                    AllowingPasswordBreachChecker.Instance);
            },
            environmentName: "Production");

    private static WebApplication BuildDevelopmentHost(
        params (string Key, string Value)[] settings) =>
        HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                var values = settings.ToDictionary(item => item.Key, item => (string?)item.Value);
                values["Hours:UseInMemoryJournal"] = "true";
                builder.Configuration.AddInMemoryCollection(values);
            },
            environmentName: "Development");

    private static async Task<(string Cookies, string AntiforgeryToken)> SignInAsync(
        HttpClient client,
        string login,
        string password)
    {
        var antiforgery = await client.GetAsync("/api/auth/antiforgery");
        var token = ReadJsonString(await antiforgery.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = SetCookie(antiforgery, "NovolisHours.Antiforgery");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login, password }),
        };
        request.Headers.Add("Cookie", CookiePair(antiforgeryCookie));
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        var response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var sessionCookie = SetCookie(response, "NovolisHours.Session");
        using var refreshedAntiforgeryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/antiforgery");
        refreshedAntiforgeryRequest.Headers.Add("Cookie", CookiePair(sessionCookie));
        antiforgery = await client.SendAsync(refreshedAntiforgeryRequest);
        token = ReadJsonString(await antiforgery.Content.ReadAsStringAsync(), "token");
        antiforgeryCookie = SetCookie(antiforgery, "NovolisHours.Antiforgery");
        return ($"{CookiePair(antiforgeryCookie)}; {CookiePair(sessionCookie)}", token);
    }

    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(name + "=", StringComparison.Ordinal));

    private static string CookiePair(string setCookie) =>
        setCookie.Split(';', 2)[0];

    private static string ReadJsonString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"JSON property '{propertyName}' was empty.");
    }
}
