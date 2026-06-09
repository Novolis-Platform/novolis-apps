using Avalonia;
using Avalonia.Win32;

namespace LiveStudio;

internal static class Program
{
    internal static LiveStudioLauncher Launcher { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Launcher = new LiveStudioLauncher();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Launcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] })
            .LogToTrace();
}
