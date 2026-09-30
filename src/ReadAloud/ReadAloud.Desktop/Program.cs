using System.Runtime.Versioning;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Audio.Voice.Platform;
using Novolis.Audio.Voice.Platform.Windows;
using Novolis.Avalonia.Diagnostics;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Logging.Diagnostics;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;
using ReadAloud.Reading;

namespace ReadAloud.Desktop;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var forwarded = new List<string>(args.Length);
        foreach (var arg in args)
        {
            const string prefix = "--baseline=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && arg.Length > prefix.Length)
                BaselineLaunch.Request(arg[prefix.Length..].Trim('"'));
            else
                forwarded.Add(arg);
        }

        using var diagnostics = new DiagnosticJournal(new DiagnosticJournalOptions
        {
            DirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Novolis",
                "ReadAloud",
                "diagnostics"),
            ApplicationName = "ReadAloud",
            StartupState = new Dictionary<string, object?>
            {
                ["platform"] = "Windows",
                ["app.version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            },
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
                    services.AddNovolisVoiceWindows(new PlatformSpeechOptions { Locale = "en-US" });
                    services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
                    services.AddReadAloudCore();
                })
                .Build();

            App.Services = host.Services;
            host.Start();
            try
            {
                BuildAvaloniaApp(diagnostics).StartWithClassicDesktopLifetime(forwarded.ToArray());
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
