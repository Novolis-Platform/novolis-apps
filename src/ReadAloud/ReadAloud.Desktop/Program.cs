using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Diagnostics;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Logging.Diagnostics;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;

namespace ReadAloud.Desktop;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var diagnostics = new DiagnosticJournal(new DiagnosticJournalOptions
        {
            DirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Novolis",
                "ReadAloud",
                "diagnostics"),
            ApplicationName = "ReadAloud",
        });
        AvaloniaDiagnostics.InstallEarly(diagnostics);
        try
        {
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddDiagnosticFileLogging(diagnostics);
                    services.AddNovolisMobileDesktop("ReadAloud");
                    services.AddNovolisMobileDesktopDiagnostics(diagnostics);
                    services.AddSingleton<NaudioMp3Player>();
                    services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<NaudioMp3Player>());
                    services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
                    services.AddReadAloudCore();
                })
                .Build();

            App.Services = host.Services;
            host.Start();
            try
            {
                BuildAvaloniaApp(diagnostics).StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                host.StopAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            diagnostics.WriteException("Program.Main", ex);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp(IDiagnosticJournal diagnostics)
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseNovolisDiagnostics(diagnostics);
}
