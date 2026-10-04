using Avalonia;
using Avalonia.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Agent.Surface;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;

namespace CadStudio;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    internal static CadSessionSurface? CadSurface { get; private set; }

    internal static AgentSurface? SceneSurface { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        CrashGuard.Install("CadStudio");

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
                    var dataRoot = CadStudioDataMigration.DefaultRoot;
                    var migration = CadStudioDataMigration.Run(dataRoot);
                    services.AddSingleton(_ => new CadEditorSettings(dataRoot));
                    services.AddSingleton(migration);
                    services.AddSingleton(sp =>
                    {
                        var settings = sp.GetRequiredService<CadEditorSettings>();
                        var session = new CadDocumentSession(settings);
                        var bus = new CadCommandBus(session);
                        var dispatcher = new CadCommandDispatcher(session, bus, settings);
                        return new CadSessionService(session, settings, bus, dispatcher)
                        {
                            AppId = "cad-studio",
                            AppTitle = "Novolis CAD Studio",
                        };
                    });
                    services.AddSingleton(_ =>
                    {
                        var scene = new SceneSessionService
                        {
                            AppId = "cad-studio-scene",
                        };
                        return scene;
                    });
                    services.AddSingleton(_ => new ShipCadSession(dataRoot));
                    services.AddSingleton(sp =>
                    {
                        var shipCad = sp.GetRequiredService<ShipCadSession>();
                        return new Novolis.Avalonia.Ship.Design.Session.ShipDesignSession(shipCad.Settings.DataRoot);
                    });
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ApplicationHost.Start();

            var cad = ApplicationHost.Services.GetRequiredService<CadSessionService>();
            var scene = ApplicationHost.Services.GetRequiredService<SceneSessionService>();

            CadSurface = CadSessionSurface.TryAttachFromEnvironment(cad);
            SceneSurface = AgentSurface.TryAttachFromEnvironment(scene, scene.Definition);

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                if (SceneSurface is not null)
                    SceneSurface.DisposeAsync().AsTask().GetAwaiter().GetResult();
                if (CadSurface is not null)
                    CadSurface.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] })
            .LogToTrace()
            .AfterSetup(_ => CrashGuard.InstallAvalonia(Avalonia.Threading.Dispatcher.UIThread));
}
