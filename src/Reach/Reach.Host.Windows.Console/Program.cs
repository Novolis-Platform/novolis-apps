using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Desktop;

namespace Novolis.Reach.Host.Windows.Console;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("ReachHostConsole");
                services.AddSingleton<ReachHostConsoleClient>();
                services.AddSingleton<ReachHostConsoleView>();
            })
            .Build();

        App.Services = host.Services;
        host.Start();
        try
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }
}
