using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using CursorRemote.Protocol;
using CursorRemote.Desktop.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CursorRemote.Desktop.Host;

public sealed class CursorRemoteHost : IHostedService, IAsyncDisposable
{
    private const int MaximumScreenshotWidth = 1600;
    private WebApplication? _app;
    private string _token = string.Empty;
    private string[] _endpoints = [];

    public bool IsRunning => _app is not null;

    public string Status { get; private set; } = "Starting host…";

    public RemoteConnectionInfo? Connection { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var addresses = TailscaleAddresses().ToArray();
        if (addresses.Length == 0)
            addresses = [IPAddress.Loopback];

        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                foreach (var address in addresses)
                    options.Listen(address, RemoteProtocol.DefaultPort);
            });

            var app = builder.Build();
            ConfigureRoutes(app);
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            _app = app;
            _endpoints = addresses
                .Select(address => $"http://{address}:{RemoteProtocol.DefaultPort}")
                .ToArray();
            var preferred = addresses.FirstOrDefault(IsTailscaleAddress);
            var endpoint = $"http://{preferred ?? addresses[0]}:{RemoteProtocol.DefaultPort}";
            Connection = new RemoteConnectionInfo(
                endpoint,
                _token,
                preferred?.ToString(),
                RemoteProtocol.Version);
            Status = preferred is null
                ? "Host is running on loopback; Tailscale was not detected."
                : "Host is ready. Pair the Android controller with the endpoint and token.";
            RaiseChanged();
        }
        catch (Exception exception)
        {
            Status = $"Host failed: {exception.Message}";
            Connection = null;
            RaiseChanged();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
            return;

        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
        Status = "Host stopped.";
        RaiseChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private void ConfigureRoutes(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1")
                && !HasValidToken(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Pairing token required.");
                return;
            }

            await next().ConfigureAwait(false);
        });

        app.MapGet("/health", () => Results.Ok(new { ok = true }));
        app.MapGet("/api/v1/status", () =>
        {
            var region = WindowsScreenCapture.ResolveRegion();
            WindowsScreenCapture.CurrentRegion = region;
            return Results.Ok(new RemoteStatusDto(
                RemoteProtocol.Version,
                Environment.MachineName,
                _endpoints,
                region.Width,
                region.Height,
                region.OriginX,
                region.OriginY,
                IsCursorRunning()));
        });
        app.MapGet("/api/v1/screen", () =>
        {
            var frame = WindowsScreenCapture.Capture(MaximumScreenshotWidth);
            return Results.File(frame.Png, "image/png");
        });
        app.MapPost("/api/v1/focus", () =>
        {
            var ok = WindowsInput.FocusCursor();
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Cursor focused." : "Cursor is not running or has no window."));
        });
        app.MapPost("/api/v1/input/click", (RemoteClickRequest request) =>
        {
            var region = WindowsScreenCapture.CurrentRegion;
            if (double.IsNaN(request.X)
                || double.IsNaN(request.Y)
                || request.X < 0
                || request.Y < 0
                || request.X >= region.Width
                || request.Y >= region.Height)
            {
                return Results.BadRequest(new RemoteOperationResponse(
                    false,
                    "Click coordinates are outside the captured screen."));
            }

            var ok = WindowsInput.Click(
                request.X,
                request.Y,
                request.Button,
                request.ClickCount);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Click sent." : "Click failed."));
        });
        app.MapPost("/api/v1/input/text", (RemoteTextRequest request) =>
        {
            if (request.Text.Length > 20_000)
                return Results.BadRequest(new RemoteOperationResponse(
                    false,
                    "Text payload is too large."));

            var ok = WindowsInput.TypeText(request.Text);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? "Text sent." : "Text input failed."));
        });
        app.MapPost("/api/v1/input/key", (RemoteKeyRequest request) =>
        {
            var ok = WindowsInput.SendKey(request.Key);
            return Results.Ok(new RemoteOperationResponse(
                ok,
                ok ? $"Key {request.Key} sent." : $"Unsupported key: {request.Key}."));
        });
    }

    private bool HasValidToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(RemoteProtocol.TokenHeader, out var values))
            return false;

        var supplied = Encoding.UTF8.GetBytes(values.ToString());
        var expected = Encoding.UTF8.GetBytes(_token);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static bool IsCursorRunning()
    {
        var processes = Process.GetProcessesByName("Cursor");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static IEnumerable<IPAddress> TailscaleAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(IsTailscaleAddress)
            .Distinct();

    private static bool IsTailscaleAddress(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 100
               && bytes[1] >= 64
               && bytes[1] <= 127;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
