using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Novolis.Hours.AspireTests;

[NotInParallel]
public sealed class HoursAspireSmokeTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);

    [Test]
    public async Task Hours_apphost_declares_the_server_storage_and_client_resources()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Novolis_Hours_AppHost>(timeout.Token)
            .WaitAsync(TimeSpan.FromSeconds(30), timeout.Token);

        await Assert.That(appHost.Resources.TryGetByName("hours", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-azurite", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-client-avalonia", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-client-maui", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-client-blazor", out _)).IsTrue();
    }

    [Test]
    public async Task Hours_resource_is_ready_and_exposes_health_endpoint()
    {
        Skip.Unless(
            string.Equals(
                Environment.GetEnvironmentVariable("NOVOLIS_HOURS_RUN_PODMAN_TESTS"),
                "1",
                StringComparison.OrdinalIgnoreCase),
            "Set NOVOLIS_HOURS_RUN_PODMAN_TESTS=1 to run the Podman-backed Aspire test.");

        using var timeout = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = timeout.Token;
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Novolis_Hours_AppHost>(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        await Assert.That(appHost.Resources.TryGetByName("hours-client-avalonia", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-client-maui", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-client-blazor", out _)).IsTrue();
        await Assert.That(appHost.Resources.TryGetByName("hours-azurite", out _)).IsTrue();

        appHost.Configuration["Parameters:hours-initial-administrator-password"] =
            "AspireFeatureTest-StrongPassword-2026!";
        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        await using var app = await appHost.BuildAsync(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);
        await app.StartAsync(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("hours", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        using var client = app.CreateHttpClient("hours");
        using var response = await client.GetAsync("/health/ready", cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
