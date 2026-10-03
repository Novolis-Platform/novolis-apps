using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Hours.Domain;
using Novolis.Hours.Server;

namespace Novolis.Hours.FeatureTests;

public sealed class HoursHostFeatureTests
{
    [Test]
    public async Task The_real_host_serves_the_spa_and_records_a_demo_admin_workday_without_mocks()
    {
        await using var app = HoursApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                    ["Hours:SeedSampleData"] = "false",
                });
            });
        await app.StartAsync();
        var client = app.GetTestClient();

        var page = await client.GetStringAsync("/");
        await Assert.That(page).Contains("Novolis Hours");

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

        var configurationRequest = new HttpRequestMessage(HttpMethod.Get, "/api/configuration/employees/admin");
        configurationRequest.Headers.Add("Cookie", $"{antiforgeryCookie}; {sessionCookie}");
        var configurationResponse = await client.SendAsync(configurationRequest);
        await Assert.That(configurationResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var configurationJson = await configurationResponse.Content.ReadAsStringAsync();
        await Assert.That(ReadJsonString(configurationJson, "settlementPolicyId")).Contains("quarterly");

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
    }

    [Test]
    public async Task The_real_host_writes_an_append_only_json_journal_when_not_in_memory()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), $"novolis-hours-feature-{Guid.NewGuid():N}");
        try
        {
            await using (var app = HoursApplication.Build(
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
                             }))
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
}
