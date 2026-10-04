using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Hours.Application;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Hours.Server;

namespace Novolis.Hours.FeatureTests;

public sealed class HoursHostFeatureTests
{
    [Test]
    public async Task The_real_host_serves_the_spa_and_records_a_demo_admin_workday_without_mocks()
    {
        await using var app = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                });
            },
            environmentName: "Development");
        await app.StartAsync();
        var client = app.GetTestClient();

        var page = await client.GetStringAsync("/");
        await Assert.That(page).Contains("Novolis Hours");
        await Assert.That(page).Contains("product web client");
        await Assert.That(page).DoesNotContain("app.js");

        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        await Assert.That(antiforgeryResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = CookieHeader(antiforgeryResponse).Single();

        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login = "admin", password = "admin" }),
        };
        login.Headers.Add("Cookie", antiforgeryCookie);
        login.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        var loginResponse = await client.SendAsync(login);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var sessionCookie = CookieHeader(loginResponse)
            .Single(cookie => cookie.StartsWith("NovolisHours.Session=", StringComparison.Ordinal));
        using var refreshedAntiforgeryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/antiforgery");
        refreshedAntiforgeryRequest.Headers.Add("Cookie", sessionCookie);
        antiforgeryResponse = await client.SendAsync(refreshedAntiforgeryRequest);
        antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        antiforgeryCookie = CookieHeader(antiforgeryResponse).Single();

        var configurationRequest = new HttpRequestMessage(HttpMethod.Get, "/api/configuration/employees/admin");
        configurationRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        var configurationResponse = await client.SendAsync(configurationRequest);
        await Assert.That(configurationResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var configurationJson = await configurationResponse.Content.ReadAsStringAsync();
        await Assert.That(ReadJsonString(configurationJson, "settlementPolicyId")).Contains("quarterly");

        var catalogRequest = new HttpRequestMessage(HttpMethod.Get, "/api/legal/preset-catalog");
        catalogRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        using var catalogDocument = JsonDocument.Parse(await (await client.SendAsync(catalogRequest)).Content.ReadAsStringAsync());
        await Assert.That(catalogDocument.RootElement.GetArrayLength()).IsEqualTo(7);

        var work = new HttpRequestMessage(HttpMethod.Post, "/api/work")
        {
            Content = JsonContent.Create(new
            {
                employeeId = "admin",
                day = "2026-10-01",
                startedAt = "09:30",
                endedAt = "21:45",
                breakStartedAt = "11:30",
                breakEndedAt = "12:00",
                financialCompensationSlices = Array.Empty<object>(),
                comment = "Late traffic followed by an emergency.",
                managerAgreementRecorded = false,
            }),
        };
        work.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        work.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        var workResponse = await client.SendAsync(work);
        await Assert.That(workResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var viewRequest = new HttpRequestMessage(HttpMethod.Get, "/api/employees/admin/view");
        viewRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        var viewResponse = await client.SendAsync(viewRequest);
        await Assert.That(viewResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var viewJson = await viewResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(viewJson);
        await Assert.That(document.RootElement.GetProperty("entries").GetArrayLength()).IsEqualTo(1);
        await Assert.That(document.RootElement.GetProperty("flexSaldo").GetString()).IsEqualTo("04:15:00");

        var queryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/query/ledger?employeeId=admin");
        queryRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        var queryResponse = await client.SendAsync(queryRequest);
        await Assert.That(queryResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var queryDocument = JsonDocument.Parse(await queryResponse.Content.ReadAsStringAsync());
        await Assert.That(queryDocument.RootElement.GetProperty("rows").GetArrayLength()).IsEqualTo(2);

        var weeklyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/query/weekly?employeeId=admin");
        weeklyRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        var weeklyResponse = await client.SendAsync(weeklyRequest);
        await Assert.That(weeklyResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var weeklyDocument = JsonDocument.Parse(await weeklyResponse.Content.ReadAsStringAsync());
        await Assert.That(weeklyDocument.RootElement.GetProperty("rows")[0].GetProperty("category").GetString())
            .IsEqualTo("iso-week");
    }

    [Test]
    public async Task The_authenticated_SignalR_hub_projects_a_durable_journal_append_from_the_real_channel()
    {
        await using var app = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                });
            },
            environmentName: "Development");
        await app.StartAsync();
        var client = app.GetTestClient();
        var session = await SignInDemoAsync(client);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/hours",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.Headers["Cookie"] = session.Cookies;
                })
            .Build();
        connection.On<JsonElement>("hoursChanged", notification => received.TrySetResult(notification));
        await connection.StartAsync();

        var work = new HttpRequestMessage(HttpMethod.Post, "/api/work")
        {
            Content = JsonContent.Create(new
            {
                employeeId = "admin",
                day = "2026-10-01",
                startedAt = "08:00",
                endedAt = "16:00",
                breakStartedAt = "11:30",
                breakEndedAt = "12:00",
                financialCompensationSlices = Array.Empty<object>(),
                comment = "Realtime projection verification.",
                managerAgreementRecorded = false,
            }),
        };
        work.Headers.Add("Cookie", session.Cookies);
        work.Headers.Add("X-Novolis-Hours-CSRF", session.AntiforgeryToken);
        await Assert.That((await client.SendAsync(work)).StatusCode).IsEqualTo(HttpStatusCode.Created);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(notification.GetProperty("employeeId").GetString()).IsEqualTo("admin");
        await Assert.That(notification.GetProperty("type").GetString()).IsEqualTo("work.registered.v1");
        await connection.StopAsync();
    }

    [Test]
    public async Task The_real_host_writes_an_append_only_json_journal_when_not_in_memory()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), $"novolis-hours-feature-{Guid.NewGuid():N}");
        try
        {
            await using (var app = HoursServerApplication.Build(
                             [],
                             builder =>
                             {
                                 builder.WebHost.UseTestServer();
                                 builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                                 {
                                     ["Hours:UseInMemoryJournal"] = "false",
                                     ["Hours:DataPath"] = dataPath,
                                     ["Hours:EnableDemoAdminCredentials"] = "true",
                                 });
                             },
                             environmentName: "Development"))
            {
                await app.StartAsync();
                var service = app.Services.GetRequiredService<HoursService>();
                await service.RegisterWorkAsync(new RegisterWorkCommand(
                    "ada",
                    new DateOnly(2026, 10, 1),
                    new TimeOnly(8, 0),
                    new TimeOnly(16, 0),
                    new TimeOnly(11, 30),
                    new TimeOnly(12, 0),
                    [],
                    "JSON persistence verification.",
                    ManagerAgreementRecorded: false,
                    Actor: HoursActor.Employee("ada")));
                var view = await service.GetEmployeeViewAsync("ada");
                await Assert.That(view.Entries.Count).IsEqualTo(1);
            }

            await Assert.That(Directory.EnumerateFiles(dataPath, "*.json", SearchOption.AllDirectories).Any()).IsTrue();
        }
        finally
        {
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task The_real_json_host_releases_its_lock_replays_the_journal_and_keeps_the_demo_flow_available_after_restart()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), $"novolis-hours-replay-{Guid.NewGuid():N}");
        try
        {
            await using (var first = BuildJsonDemoHost(dataPath))
            {
                await first.StartAsync();
                var client = first.GetTestClient();
                var session = await SignInDemoAsync(client);
                var work = new HttpRequestMessage(HttpMethod.Post, "/api/work")
                {
                    Content = JsonContent.Create(new
                    {
                        employeeId = "admin",
                        day = "2026-10-01",
                        startedAt = "08:00",
                        endedAt = "16:00",
                        breakStartedAt = "11:30",
                        breakEndedAt = "12:00",
                        financialCompensationSlices = Array.Empty<object>(),
                        comment = "Restart replay verification.",
                        managerAgreementRecorded = false,
                    }),
                };
                work.Headers.Add("Cookie", session.Cookies);
                work.Headers.Add("X-Novolis-Hours-CSRF", session.AntiforgeryToken);
                await Assert.That((await client.SendAsync(work)).StatusCode).IsEqualTo(HttpStatusCode.Created);
            }

            await using (var second = BuildJsonDemoHost(dataPath))
            {
                await second.StartAsync();
                var client = second.GetTestClient();
                var session = await SignInDemoAsync(client);
                var view = new HttpRequestMessage(HttpMethod.Get, "/api/employees/admin/view");
                view.Headers.Add("Cookie", session.Cookies);
                using var document = JsonDocument.Parse(await (await client.SendAsync(view)).Content.ReadAsStringAsync());
                await Assert.That(document.RootElement.GetProperty("entries").GetArrayLength()).IsEqualTo(1);
                await Assert.That(document.RootElement.GetProperty("entries")[0].GetProperty("comment").GetString())
                    .IsEqualTo("Restart replay verification.");
            }
        }
        finally
        {
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task The_real_azure_table_host_round_trips_security_profiles_and_work_events_without_mocks()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "NOVOLIS_HOURS_AZURITE_CONNECTION_STRING");
        Skip.Unless(
            !string.IsNullOrWhiteSpace(connectionString),
            "Set NOVOLIS_HOURS_AZURITE_CONNECTION_STRING to an Azurite Table connection string.");

        var tablePrefix = $"novolishourstest{Guid.NewGuid():N}";
        const string password = "AzureFeatureTest-StrongPassword-2026!";

        await using (var first = BuildAzureHost(connectionString!, tablePrefix, password))
        {
            await first.StartAsync();
            var client = first.GetTestClient();
            var session = await SignInAsync(client, "admin", password);

            var work = new HttpRequestMessage(HttpMethod.Post, "/api/work")
            {
                Content = JsonContent.Create(new
                {
                    employeeId = "admin",
                    day = "2026-10-01",
                    startedAt = "08:00",
                    endedAt = "16:00",
                    breakStartedAt = "11:30",
                    breakEndedAt = "12:00",
                    financialCompensationSlices = Array.Empty<object>(),
                    comment = "Azurite round-trip verification.",
                    managerAgreementRecorded = false,
                }),
            };
            work.Headers.Add("Cookie", session.Cookies);
            work.Headers.Add("X-Novolis-Hours-CSRF", session.AntiforgeryToken);

            await Assert.That((await client.SendAsync(work)).StatusCode)
                .IsEqualTo(HttpStatusCode.Created);
        }

        await using (var second = BuildAzureHost(connectionString!, tablePrefix, password))
        {
            await second.StartAsync();
            var client = second.GetTestClient();
            var session = await SignInAsync(client, "admin", password);
            var view = new HttpRequestMessage(HttpMethod.Get, "/api/employees/admin/view");
            view.Headers.Add("Cookie", session.Cookies);

            using var document = JsonDocument.Parse(
                await (await client.SendAsync(view)).Content.ReadAsStringAsync());
            await Assert.That(document.RootElement.GetProperty("entries").GetArrayLength())
                .IsEqualTo(1);
            await Assert.That(
                    document.RootElement.GetProperty("entries")[0].GetProperty("comment").GetString())
                .IsEqualTo("Azurite round-trip verification.");
        }
    }

    [Test]
    public async Task An_administrator_can_persist_individual_policy_and_work_fraction_settings_before_registration()
    {
        await using var app = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                });
            },
            environmentName: "Development");
        await app.StartAsync();
        var directory = app.Services.GetRequiredService<HoursUserDirectory>();
        await directory.SaveAsync(new HoursUserDocument(
            Guid.NewGuid(),
            "ada",
            "ada",
            "Ada Lovelace",
            HoursActorRole.Employee));

        var client = app.GetTestClient();
        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        var antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = AntiforgeryCookie(antiforgeryResponse);
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login = "admin", password = "admin" }),
        };
        login.Headers.Add("Cookie", antiforgeryCookie);
        login.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        var sessionCookie = CookieHeader(await client.SendAsync(login))
            .Single(cookie => cookie.StartsWith("NovolisHours.Session=", StringComparison.Ordinal));
        using var refreshedAntiforgeryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/antiforgery");
        refreshedAntiforgeryRequest.Headers.Add("Cookie", sessionCookie);
        antiforgeryResponse = await client.SendAsync(refreshedAntiforgeryRequest);
        antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        antiforgeryCookie = CookieHeader(antiforgeryResponse).Single();
        var cookies = $"{antiforgeryCookie}; {sessionCookie}";

        var settings = new HttpRequestMessage(HttpMethod.Put, "/api/admin/users/ada/worktime-settings")
        {
            Content = JsonContent.Create(new
            {
                legalPresetId = "norway.state.flex",
                workFraction = 0.5m,
                expectedIntervalOverrideStart = "09:00",
                expectedIntervalOverrideEnd = "17:00",
            }),
        };
        settings.Headers.Add("Cookie", cookies);
        settings.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        await Assert.That((await client.SendAsync(settings)).StatusCode).IsEqualTo(HttpStatusCode.OK);

        var configuration = new HttpRequestMessage(HttpMethod.Get, "/api/configuration/employees/ada");
        configuration.Headers.Add("Cookie", cookies);
        using var configurationDocument = JsonDocument.Parse(
            await (await client.SendAsync(configuration)).Content.ReadAsStringAsync());
        await Assert.That(configurationDocument.RootElement.GetProperty("legalPreset").GetProperty("id").GetString())
            .IsEqualTo("norway.state.flex");
        await Assert.That(configurationDocument.RootElement.GetProperty("individualSettings").GetProperty("workFraction").GetDecimal())
            .IsEqualTo(0.5m);

        var work = new HttpRequestMessage(HttpMethod.Post, "/api/work")
        {
            Content = JsonContent.Create(new
            {
                employeeId = "ada",
                day = "2026-10-01",
                startedAt = "09:00",
                endedAt = "17:00",
                breakStartedAt = "11:30",
                breakEndedAt = "12:00",
                financialCompensationSlices = Array.Empty<object>(),
                comment = "Half-time employment settings verification.",
                managerAgreementRecorded = false,
            }),
        };
        work.Headers.Add("Cookie", cookies);
        work.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        await Assert.That((await client.SendAsync(work)).StatusCode).IsEqualTo(HttpStatusCode.Created);

        var view = new HttpRequestMessage(HttpMethod.Get, "/api/employees/ada/view");
        view.Headers.Add("Cookie", cookies);
        using var viewDocument = JsonDocument.Parse(await (await client.SendAsync(view)).Content.ReadAsStringAsync());
        await Assert.That(viewDocument.RootElement.GetProperty("entries")[0].GetProperty("expectedDuration").GetString())
            .IsEqualTo("03:45:00");
    }

    [Test]
    public async Task A_non_demo_host_provisions_its_first_administrator_through_the_real_security_path()
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
                });
            },
            environmentName: "Development");
        await app.StartAsync();
        var client = app.GetTestClient();

        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        var antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = CookieHeader(antiforgeryResponse).Single();
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login = "admin", password = "Cobalt-Raven-45!" }),
        };
        login.Headers.Add("Cookie", antiforgeryCookie);
        login.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);

        var response = await client.SendAsync(login);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(document.RootElement.GetProperty("isDemoAdministrator").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task A_non_demo_host_refuses_to_start_without_an_existing_administrator_or_bootstrap_secret()
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
                });
            },
            environmentName: "Development");
        Func<Task> start = () => app.StartAsync();

        await Assert.That(start).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Html_export_includes_policy_traceability_and_escapes_recorded_comments()
    {
        var service = new HoursService(
            new InMemoryHoursJournal(),
            NorwegianHoursPolicy.Create(year: 2026));
        await service.RegisterWorkAsync(new RegisterWorkCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            new TimeOnly(9, 30),
            new TimeOnly(21, 45),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [],
            "<script>do-not-execute</script>",
            ManagerAgreementRecorded: false,
            Actor: HoursActor.Employee("ada")));

        var report = new HoursHtmlReportExporter().Export(await service.GetEmployeeViewAsync("ada"));

        await Assert.That(report).Contains("<!DOCTYPE html>");
        await Assert.That(report).Contains("norway.private.flex");
        await Assert.That(report).Contains("working-day.envelope");
        await Assert.That(report).Contains("&lt;script&gt;do-not-execute&lt;/script&gt;");
        await Assert.That(report).DoesNotContain("<script>do-not-execute</script>");
    }

    [Test]
    public async Task An_authenticated_HR_level_endpoint_resolution_preserves_the_dispute_and_applies_only_the_selected_outcome()
    {
        await using var app = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                });
            },
            environmentName: "Development");
        await app.StartAsync();
        var hours = app.Services.GetRequiredService<HoursService>();
        var adjustment = await hours.ProposeAdjustmentAsync(new ProposeAdjustmentCommand(
            "ada",
            new DateOnly(2026, 10, 1),
            TimeSpan.FromHours(-3),
            HoursAdjustmentReason.ConvertToFinancialCompensation,
            "Proposed conversion.",
            HoursActor.Manager("mia")));
        await hours.RespondToAdjustmentAsync(new RespondToAdjustmentCommand(
            adjustment.Id,
            HoursAdjustmentResponse.Dispute,
            "Please escalate this adjustment.",
            HoursActor.Employee("ada")));

        var client = app.GetTestClient();
        var session = await SignInDemoAsync(client);
        var resolution = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/adjustments/{adjustment.Id}/resolve")
        {
            Content = JsonContent.Create(new
            {
                resolution = HoursAdjustmentResolution.Reject,
                comment = "No documented manager agreement was found.",
            }),
        };
        resolution.Headers.Add("Cookie", session.Cookies);
        resolution.Headers.Add("X-Novolis-Hours-CSRF", session.AntiforgeryToken);

        await Assert.That((await client.SendAsync(resolution)).StatusCode).IsEqualTo(HttpStatusCode.OK);
        var view = await hours.GetEmployeeViewAsync("ada");
        await Assert.That(view.Adjustments.Single().State).IsEqualTo(HoursAdjustmentState.ResolvedRejected);
        await Assert.That(view.FlexSaldo).IsEqualTo(TimeSpan.Zero);
        await Assert.That(view.Anomalies.Single(anomaly => anomaly.Code == "adjustment.disputed").Message)
            .Contains("resolved as rejected");
    }

    private static string ReadJsonString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"JSON property '{propertyName}' was empty.");
    }

    private static IReadOnlyList<string> CookieHeader(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .ToArray();

    private static WebApplication BuildJsonDemoHost(string dataPath) =>
        HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "false",
                    ["Hours:DataPath"] = dataPath,
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                });
            },
            environmentName: "Development");

    private static WebApplication BuildAzureHost(
        string connectionString,
        string tablePrefix,
        string administratorPassword) =>
        HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:StorageProvider"] = "azure-tables",
                    ["Hours:AzureTablesConnectionString"] = connectionString,
                    ["Hours:AzureTablesTablePrefix"] = tablePrefix,
                    ["Hours:InitialAdministratorPassword"] = administratorPassword,
                    ["Hours:EnableDemoAdminCredentials"] = "false",
                });
            },
            environmentName: "Development");

    private static async Task<(string Cookies, string AntiforgeryToken)> SignInDemoAsync(HttpClient client)
        => await SignInAsync(client, "admin", "admin");

    private static async Task<(string Cookies, string AntiforgeryToken)> SignInAsync(
        HttpClient client,
        string loginName,
        string password)
    {
        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        var antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        var antiforgeryCookie = CookieHeader(antiforgeryResponse).Single();
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { login = loginName, password }),
        };
        login.Headers.Add("Cookie", antiforgeryCookie);
        login.Headers.Add("X-Novolis-Hours-CSRF", antiforgeryToken);
        var response = await client.SendAsync(login);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var sessionCookie = CookieHeader(response)
            .Single(cookie => cookie.StartsWith("NovolisHours.Session=", StringComparison.Ordinal));
        using var refreshedAntiforgeryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/antiforgery");
        refreshedAntiforgeryRequest.Headers.Add("Cookie", sessionCookie);
        antiforgeryResponse = await client.SendAsync(refreshedAntiforgeryRequest);
        antiforgeryToken = ReadJsonString(await antiforgeryResponse.Content.ReadAsStringAsync(), "token");
        antiforgeryCookie = AntiforgeryCookie(antiforgeryResponse);
        return ($"{antiforgeryCookie}; {sessionCookie}", antiforgeryToken);
    }

    private static string AntiforgeryCookie(HttpResponseMessage response) =>
        CookieHeader(response)
            .Single(cookie => cookie.Contains(".Antiforgery=", StringComparison.Ordinal));
}
