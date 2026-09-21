using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Reach.Client;

namespace Novolis.Reach.Client.Windows;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("ReachClientWindows");
                services.AddSingleton<ReachClientSession>();
                services.AddSingleton<IReachVideoPresenter, WindowsReachVideoPresenter>();
                services.AddSingleton<IReachAudioPresenter, WindowsReachAudioPresenter>();
                services.AddSingleton<ReachClientView>();
            })
            .Build();

        App.Services = host.Services;
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

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
