using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Desktop;
using PresenceLedger.App;
using PresenceLedgerApp = PresenceLedger.App.App;

namespace PresenceLedger.Desktop;

/// <summary>Local Windows UI head; it does not run background presence tracking.</summary>
[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("PresenceLedger");
                services.AddPresenceLedger();
            })
            .Build();

        PresenceLedgerApp.Services = host.Services;
        host.Start();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<PresenceLedgerApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
