using Avalonia;
using BooksWriterStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Manuscript.Export.Audio;

namespace BooksWriterStudio;

internal static class Program
{
    internal static string? StartupFolder { get; private set; }

    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        CrashLog.Install();

        try
        {
            if (args.Length > 0 && Directory.Exists(args[0]))
                StartupFolder = Path.GetFullPath(args[0]);

            ApplicationHost = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<WriterSettingsStore>();
                    services.AddSingleton<WriterSession>();
                    services.AddSingleton<SpellService>();
                    services.AddSingleton<EdgeTtsSynthesizer>();
                    services.AddSingleton<ISynthesizer>(sp => sp.GetRequiredService<EdgeTtsSynthesizer>());
                    services.AddSingleton<NaudioMp3Player>();
                    services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<NaudioMp3Player>());
                    services.AddSingleton<SpeechPreview>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ApplicationHost.Start();

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                ApplicationHost.StopAsync().GetAwaiter().GetResult();
                ApplicationHost.Services.GetService<NaudioMp3Player>()?.Dispose();
                ApplicationHost.Services.GetService<EdgeTtsSynthesizer>()?.Dispose();
            }
        }
        catch (Exception ex)
        {
            CrashLog.WriteAndOpen(ex, "Program.Main");
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ => CrashLog.InstallAvalonia(Avalonia.Threading.Dispatcher.UIThread));
}
