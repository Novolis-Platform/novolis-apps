using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Transports.Tailscale;
using Novolis.Windows.Sessions;

namespace Novolis.Reach.Host.Windows.Service;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Novolis Reach Host";
        });
        builder.Services.AddSingleton<TailscaleAddressEnumerator>();
        builder.Services.AddSingleton<WindowsSessionManager>();
        builder.Services.AddSingleton<ReachHostService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ReachHostService>());
        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
