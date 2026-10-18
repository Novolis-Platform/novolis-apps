using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Reach.Host.Server;

/// <summary>Hosts the optional HTTPS SignalR status channel.</summary>
internal sealed class ReachSignalRSideChannelHost(
    ReachHostRuntime runtime,
    X509Certificate2 certificate) : IAsyncDisposable
{
    private WebApplication? _application;

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ApplicationName = typeof(ReachSignalRSideChannelHost)
                    .Assembly
                    .GetName()
                    .Name,
            });
        builder.WebHost.ConfigureKestrel(
            options =>
            {
                options.ListenAnyIP(
                    ReachProtocol.SideChannelPort,
                    listen => listen.UseHttps(certificate));
            });
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(runtime);

        var application = builder.Build();
        application.MapGet(
            "/health",
            () => Results.Ok(new { service = ReachProtocol.AppId }));
        application.MapHub<ReachSideChannelHub>(
            ReachProtocol.SideChannelHubPath);

        try
        {
            await application.StartAsync(cancellationToken)
                .ConfigureAwait(false);
            _application = application;
            Volatile.Write(ref runtime.SideChannelStarted, 1);
            runtime.WriteLog(
                $"SignalR side channel listening on HTTPS port "
                + $"{ReachProtocol.SideChannelPort}.");
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref runtime.SideChannelStarted, 0);
        var application = Interlocked.Exchange(ref _application, null);
        if (application is null)
            return;

        try
        {
            await application.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await application.DisposeAsync().ConfigureAwait(false);
        }
    }
}
