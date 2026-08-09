using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Avalonia.Agent;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace SketchStudio;

internal static class Program
{
    internal static IHost ApplicationHost { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = SmokeRunner.Run();
            return;
        }

        if (args.Any(a => string.Equals(a, "--agent-probe", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Environment.ExitCode = AgentProbe.RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 99;
            }

            return;
        }

        CrashGuard.Install("SketchStudio");

        ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<SketchStudioSettings>();
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
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
