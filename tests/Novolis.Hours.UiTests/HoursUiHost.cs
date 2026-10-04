using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Server;

namespace Novolis.Hours.UiTests;

/// <summary>Kestrel Hours API plus the built Blazor WASM static web assets, both on localhost.</summary>
public sealed class HoursUiHost : IAsyncDisposable
{
    private WebApplication? server;
    private WebApplication? client;

    /// <summary>API origin used as the Hours service URL.</summary>
    public required Uri ApiUri { get; init; }

    /// <summary>Blazor origin Playwright opens.</summary>
    public required Uri ClientUri { get; init; }

    /// <summary>Starts both hosts on free localhost ports.</summary>
    public static async Task<HoursUiHost> StartAsync()
    {
        var apiPort = FreePort();
        var clientPort = FreePort();
        var apiUri = new Uri($"http://localhost:{apiPort}/");
        var clientUri = new Uri($"http://localhost:{clientPort}/");
        var dataPath = Path.Combine(Path.GetTempPath(), "NovolisHoursUi", Guid.NewGuid().ToString("N"));
        var server = HoursServerApplication.Build(
            [],
            builder =>
            {
                builder.WebHost.UseUrls(apiUri.AbsoluteUri.TrimEnd('/'));
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hours:UseInMemoryJournal"] = "true",
                    ["Hours:StorageProvider"] = "in-memory",
                    ["Hours:DataPath"] = dataPath,
                    ["Hours:EnableDemoAdminCredentials"] = "true",
                    ["Hours:EnableAcceptanceSeed"] = "true",
                    ["Hours:AcceptanceSeedPassword"] = "Acceptance-2026-Strong!",
                    ["Hours:InitialAdministratorPassword"] = "Bootstrap-2026-Strong!",
                    ["Hours:AllowedClientOrigins:0"] = clientUri.AbsoluteUri.TrimEnd('/'),
                });
            },
            environmentName: "Development");
        await server.StartAsync();

        WebApplication? client = null;
        try
        {
            client = StartBlazor(clientUri);
            await client.StartAsync();
            await WaitForAsync(clientUri);
            return new HoursUiHost
            {
                ApiUri = apiUri,
                ClientUri = clientUri,
                server = server,
                client = client,
            };
        }
        catch
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }

            await server.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (client is not null)
        {
            await client.DisposeAsync();
            client = null;
        }

        if (server is not null)
        {
            await server.DisposeAsync();
            server = null;
        }
    }

    private static WebApplication StartBlazor(Uri clientUri)
    {
        var output = Path.GetDirectoryName(FindExisting(Path.Combine(
            "artifacts",
            "bin",
            "Novolis.Hours.Client.Blazor",
            "debug",
            "Novolis.Hours.Client.Blazor.staticwebassets.runtime.json")))
            ?? throw new InvalidOperationException("The Hours Blazor output folder was not found.");
        var projectWww = Path.GetDirectoryName(FindExisting(Path.Combine(
            "src",
            "NovolisHours",
            "Novolis.Hours.Client.Blazor",
            "wwwroot",
            "index.html")))
            ?? throw new InvalidOperationException("The Hours Blazor wwwroot was not found.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = "Novolis.Hours.Client.Blazor",
            ContentRootPath = output,
            WebRootPath = Path.Combine(output, "wwwroot"),
            EnvironmentName = "Development",
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(clientUri.AbsoluteUri.TrimEnd('/'));
        builder.WebHost.UseStaticWebAssets();
        var app = builder.Build();
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".dat"] = "application/octet-stream";
        types.Mappings[".pdb"] = "application/octet-stream";
        var projectFiles = new PhysicalFileProvider(projectWww);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = projectFiles });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = projectFiles, ContentTypeProvider = types });
        app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = types });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = projectFiles });
        return app;
    }

    private static string FindExisting(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Hours UI static file '{relativePath}' was not found.");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForAsync(Uri uri)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(uri);
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Hours Blazor did not listen on {uri}.");
    }
}
