using Avalonia;

namespace Novolis.Hours.Client.Avalonia;

/// <summary>Starts the Novolis Hours Avalonia client shell.</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the platform-specific Avalonia application.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
