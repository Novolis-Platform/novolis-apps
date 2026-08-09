using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Ship;

namespace ShipDesigner;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;
    internal static CadSessionSurface? CadSession { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        CrashGuard.Install("ShipDesigner");

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
                    services.AddSingleton(_ => new CadEditorSettings(
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Novolis",
                            "Ship Designer")));
                    services.AddSingleton<CadDocumentSession>();
                    services.AddSingleton<CadCommandBus>();
                    services.AddSingleton(sp =>
                    {
                        var session = sp.GetRequiredService<CadDocumentSession>();
                        var settings = sp.GetRequiredService<CadEditorSettings>();
                        var bus = sp.GetRequiredService<CadCommandBus>();
                        var dispatcher = new CadCommandDispatcher(session, bus, settings);
                        return new CadSessionService(session, settings, bus, dispatcher)
                        {
                            AppId = "ship-designer",
                            AppTitle = "Ship Designer",
                        };
                    });
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ApplicationHost.Start();

            var cad = ApplicationHost.Services.GetRequiredService<CadSessionService>();
            ShipChrome.Attach(cad);
            CadSession = CadSessionSurface.AttachAll(cad);

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                if (CadSession is not null)
                    CadSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
