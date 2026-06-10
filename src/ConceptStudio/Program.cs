using Avalonia;
using ConceptStudio.Core;
using ConceptStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConceptStudio;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ConceptSettingsStore>();
                services.AddSingleton<ConceptSceneStore>();
                services.AddSingleton<ConceptSession>();
                services.AddSingleton<PathTraceViewport>();
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
