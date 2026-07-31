using Avalonia;
using Avalonia.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Agent.Surface;
using Novolis.Avalonia._3D.Session;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;

namespace CadStudio3D;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    internal static CadSessionSurface? CadSurface { get; private set; }

    internal static AgentSurface? SceneSurface { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        CrashGuard.Install("CadStudio3D");

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
                            "CAD Studio 3D")));
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
                            AppId = "cad-studio-3d",
                            AppTitle = "Novolis CAD Studio 3D",
                        };
                    });
                    services.AddSingleton(_ =>
                    {
                        var scene = new SceneSessionService
                        {
                            AppId = "cad-studio-3d-scene",
                        };
                        return scene;
                    });
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ApplicationHost.Start();

            var cad = ApplicationHost.Services.GetRequiredService<CadSessionService>();
            var scene = ApplicationHost.Services.GetRequiredService<SceneSessionService>();

            CadSurface = CadSessionSurface.AttachAll(cad);
            SceneSurface = AgentSurface.AttachAll(scene, scene.Definition)
                            ?? AgentSurface.TryAttachFromEnvironment(scene, scene.Definition);

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
