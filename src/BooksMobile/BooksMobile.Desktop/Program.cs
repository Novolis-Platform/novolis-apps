using System.Runtime.Versioning;
using Avalonia;
using BooksMobile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Manuscript.Export.Audio;
using Novolis.Avalonia.Mobile.Desktop;

namespace BooksMobile.Desktop;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("BooksMobile");
                services.AddSingleton<NaudioMp3Player>();
                services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<NaudioMp3Player>());
                services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
                services.AddBooksMobileCore();
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
            host.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
