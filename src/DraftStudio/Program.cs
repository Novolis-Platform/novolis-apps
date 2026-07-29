using Avalonia;
using DraftStudio.Commands;
using DraftStudio.Core;
using DraftStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Agent;

namespace DraftStudio;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        CrashGuard.Install("DraftStudio");

        if (args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = SmokeRunner.Run();
            return;
        }

        try
        {
            ApplicationHost = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<DraftSettingsStore>();
                    services.AddSingleton<DraftSession>();
                    services.AddSingleton<DraftCommandBus>();
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
        catch (Exception ex)
        {
            CrashGuard.Report(ex, "Program.Main", openEditor: true, writeMiniDump: true);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ => CrashGuard.InstallAvalonia(Avalonia.Threading.Dispatcher.UIThread));
}
