using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Novolis.Hours.Client;
using Novolis.Hours.Server;

namespace Novolis.Hours.FeatureTests;

public sealed class HoursClientFeatureTests
{
    [Test]
    public async Task The_native_api_client_uses_the_real_cookie_and_antiforgery_flow_to_post_and_read_work()
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
                });
            },
            environmentName: "Development");
        await app.StartAsync();

        using var client = new HoursApiClient(new HttpClient(
            new HoursSessionHandler(app.GetTestServer().CreateHandler()),
            disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost/"),
        });

        var signedIn = await client.SignInAsync("admin", "admin");
        await Assert.That(signedIn.EmployeeId).IsEqualTo("admin");
        await Assert.That(signedIn.IsDemoAdministrator).IsTrue();

        var currentUser = await client.GetCurrentUserAsync();
        await Assert.That(currentUser.Role).IsEqualTo(HoursClientRole.Administrator);

        await client.RegisterWorkAsync(new HoursWorkRegistration(
            "admin",
            new DateOnly(2026, 10, 1),
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            new TimeOnly(11, 30),
            new TimeOnly(12, 0),
            [],
            "Native client HTTP verification.",
            ManagerAgreementRecorded: false));

        var summary = await client.GetEmployeeSummaryAsync("admin");
        await Assert.That(summary.PresenceRecordCount).IsEqualTo(1);
        await Assert.That(summary.FlexSaldo).IsEqualTo(TimeSpan.Zero);
    }
}
