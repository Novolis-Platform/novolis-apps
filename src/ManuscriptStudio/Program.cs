using Avalonia;
using ManuscriptStudio.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;

namespace ManuscriptStudio;

internal static class Program
{
    internal static string? StartupFolder { get; private set; }

    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        if (args.Contains("--smoke-calypso", StringComparer.OrdinalIgnoreCase))
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "ManuscriptStudio-smoke");
            Environment.ExitCode = Smoke.CalypsoViewsSmoke.Run(dataRoot);
            return;
        }

        if (args.Length > 0 && Directory.Exists(args[0]))
            StartupFolder = Path.GetFullPath(args[0]);

        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ManuscriptSettingsStore>();
                services.AddSingleton<EditorSession>();
                services.AddSingleton<ManuscriptExtensionRegistry>();
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
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
