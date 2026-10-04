using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Novolis.Hours.Client;
using Novolis.Hours.Server;

namespace Novolis.Hours.FeatureTests;

/// <summary>Real TestServer composition with the named acceptance identities enabled.</summary>
public sealed class HoursAcceptanceFixture : IAsyncDisposable
{
    private const string AcceptancePassword = "Acceptance-2026-Strong!";
    private WebApplication? application;

    /// <summary>Starts a complete development Hours host for one acceptance test.</summary>
    public static async Task<HoursAcceptanceFixture> StartAsync(
        string storageProvider = "in-memory",
        string? dataPath = null,
        string? azureTablePrefix = null)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            dataPath = Path.Combine(
                Path.GetTempPath(),
                "NovolisHoursAcceptance",
                Guid.NewGuid().ToString("N"));
        }

        var fixture = new HoursAcceptanceFixture();
        fixture.DataPath = dataPath;
        fixture.application = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = string.Equals(storageProvider, "in-memory", StringComparison.OrdinalIgnoreCase).ToString(),
                    ["Hours:StorageProvider"] = storageProvider,
                    ["Hours:DataPath"] = dataPath,
                    ["Hours:AzureTablesConnectionString"] =
                        Environment.GetEnvironmentVariable("NOVOLIS_HOURS_AZURITE_CONNECTION_STRING"),
                    ["Hours:AzureTablesTablePrefix"] =
                        azureTablePrefix ?? $"novolishoursacceptance{Guid.NewGuid():N}",
                    ["Hours:EnableDemoAdminCredentials"] = "false",
                    ["Hours:EnableAcceptanceSeed"] = "true",
                    ["Hours:AcceptanceSeedPassword"] = AcceptancePassword,
                    ["Hours:InitialAdministratorPassword"] = "Bootstrap-2026-Strong!",
                    ["Hours:InitialAdministratorLogin"] = "acceptance-admin",
                    ["Hours:InitialAdministratorEmployeeId"] = "acceptance-admin",
                    ["Hours:InitialAdministratorDisplayName"] = "Acceptance Administrator",
                });
            },
            environmentName: "Development");
        await fixture.application.StartAsync();
        return fixture;
    }

    /// <summary>Gets the JSON data path when this fixture uses persisted storage.</summary>
    public string? DataPath { get; private set; }

    /// <summary>Creates and authenticates an isolated real HTTP client for one identity.</summary>
    public Task<HoursApiClient> ConnectAsync(string login) =>
        ConnectAsync(login, AcceptancePassword);

    /// <summary>Signs in the host's provisioned administrator.</summary>
    public Task<HoursApiClient> ConnectAdministratorAsync() =>
        ConnectAsync("acceptance-admin", "Bootstrap-2026-Strong!");

    /// <summary>Creates and authenticates an isolated real HTTP client with an explicit password.</summary>
    public async Task<HoursApiClient> ConnectAsync(string login, string password)
    {
        if (application is null)
        {
            throw new InvalidOperationException("The acceptance fixture has not started.");
        }

        var client = new HoursApiClient(new HttpClient(
            new HoursSessionHandler(application.GetTestServer().CreateHandler()),
            disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost/"),
        });
        try
        {
            await client.SignInAsync(login, password);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (application is not null)
        {
            await application.DisposeAsync();
            application = null;
        }
    }
}
