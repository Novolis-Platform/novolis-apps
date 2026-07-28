using Avalonia;
using Avalonia.Win32;

namespace LiveStudio;

internal static class Program
{
    internal static LiveStudioRuntime Runtime { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--headless-demo", StringComparison.OrdinalIgnoreCase)))
        {
            RunHeadlessDemoAsync().GetAwaiter().GetResult();
            return;
        }

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

    static async Task RunHeadlessDemoAsync()
    {
        Console.WriteLine("LiveStudio headless demo starting...");
        await using var session = new LiveStudioSession();

        LiveStudioState? last = null;
        session.StateChanged += state =>
        {
            last = state;
            Console.WriteLine(
                $"[{DateTimeOffset.Now:HH:mm:ss.fff}] conn={state.ConnectionStatus} | act={state.ActivityStatus} | preset={state.CurrentPresetName} | host={state.IsHostConnected} | beat={state.Snapshot?.Beat:0.###} | err={state.ErrorMessage}");
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await session.StartAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"START FAILED: {ex.Message}");
            Environment.ExitCode = 2;
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (last is { IsHostConnected: true, Snapshot.ActiveProgramId: not null })
            {
                Console.WriteLine($"OK connected playing {last.CurrentPresetName} @ {last.Snapshot.Bpm} BPM beat={last.Snapshot.Beat:0.##}");
                Environment.ExitCode = 0;
                return;
            }

            if (last is { HasFatalLauncherError: true })
            {
                Console.Error.WriteLine($"FATAL: {last.ErrorMessage}");
                Environment.ExitCode = 3;
                return;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        Console.Error.WriteLine("TIMEOUT waiting for an active live program.");
        Environment.ExitCode = 4;
    }
}
