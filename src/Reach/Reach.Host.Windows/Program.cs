using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Reach.Host.Windows.Session;
using Novolis.Windows.Audio;
using Novolis.Windows.Clipboard;
using Novolis.Windows.Display;
using Novolis.Windows.Input;

namespace Novolis.Reach.Host.Windows;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: @"Local\Novolis.Reach.Host.Windows",
            createdNew: out var createdNew);
        if (!createdNew)
            return;

        App.StartInTray = args.Any(static arg =>
            string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase));
        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNovolisMobileDesktop("ReachHost");
                services.AddSingleton<WindowsInputController>();
                services.AddSingleton<WindowsClipboardService>();
                services.AddSingleton<WindowsDisplayTopology>();
                services.AddSingleton<WindowsLoopbackAudioCapture>();
                services.AddSingleton<ReachSessionHost>();
                services.AddHostedService(sp => sp.GetRequiredService<ReachSessionHost>());
                services.AddSingleton<ReachHostClient>();
                services.AddSingleton<ReachHostTrayController>();
                services.AddSingleton<ReachHostView>();
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
            host.Services.GetService<ReachHostTrayController>()?.Dispose();
            host.StopAsync().GetAwaiter().GetResult();
        }
    }
}
