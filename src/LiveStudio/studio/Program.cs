using Avalonia;
using Avalonia.Win32;

namespace LiveStudio;

internal static class Program
{
    internal static LiveStudioRuntime Runtime { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Runtime = new LiveStudioRuntime();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] })
            .LogToTrace();
}
