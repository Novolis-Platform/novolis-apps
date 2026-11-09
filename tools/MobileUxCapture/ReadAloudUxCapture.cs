using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Avalonia.Speech;
using Novolis.Logging.Diagnostics;
using Novolis.Manuscript.Export.Audio;
using ReadAloud;
using ReadAloud.Views;

namespace MobileUxCapture;

/// <summary>Read Aloud speech chrome — every provider view × phone/desktop × light/dark.</summary>
[SupportedOSPlatform("windows")]
internal static class ReadAloudUxCapture
{
    public static void CaptureAll(string outDir)
    {
        Directory.CreateDirectory(outDir);

        foreach (var form in UxFormFactor.All)
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                CaptureJourney(outDir, form, theme);
        }

        UxCaptureSession.ResetTheme();
    }

    static void CaptureJourney(string outDir, UxFormFactor form, ThemeVariant theme)
    {
        Application.Current!.RequestedThemeVariant = theme;
        var themeId = theme == ThemeVariant.Dark ? "dark" : "light";

        using var diagnostics = new DiagnosticJournal(new DiagnosticJournalOptions
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "novolis-ux-readaloud", "diagnostics"),
            ApplicationName = "ReadAloudUxCapture",
        });

        var services = new ServiceCollection();
        services.AddDiagnosticFileLogging(diagnostics);
        services.AddNovolisMobileDesktop("ReadAloudUxCapture");
        services.AddNovolisMobileDesktopDiagnostics(diagnostics);
        // Ephemeral store so Flip8 captures do not pick up a machine Azure key.
        services.AddSingleton<ISecureTokenStore, MemoryTokenStore>();
        services.AddSingleton<IAppDataPaths>(_ => new DesktopAppDataPaths("ReadAloudUxCapture"));
        services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
        services.AddSingleton<IAudioPlayer, SilentAudioPlayer>();
        services.AddSingleton<IVoiceService, SilentVoice>();
        services.AddReadAloudCore();
        using var provider = services.BuildServiceProvider();

        var workspace = provider.GetRequiredService<SpeechWorkspace>();
        var view = provider.GetRequiredService<MainView>();
        var window = new Window
        {
            Width = form.Width,
            Height = form.Height,
            Background = GraphicalProfile.BackgroundBrush,
            Content = view,
            Title = $"Read Aloud {form.Id} {themeId}",
        };
        window.Show();
        UxCaptureSession.Pump(900);
        workspace.SelectEdgeAsync().GetAwaiter().GetResult();
        UxCaptureSession.Pump(250);

        Save(window, outDir, "edge", form, themeId);

        workspace.SelectAzureAsync().GetAwaiter().GetResult();
        UxCaptureSession.Pump(350);
        Save(window, outDir, "azure-setup", form, themeId);

        workspace.SelectDeviceAsync().GetAwaiter().GetResult();
        UxCaptureSession.Pump(250);
        Save(window, outDir, "device", form, themeId);

        workspace.SelectEdgeAsync().GetAwaiter().GetResult();
        UxCaptureSession.Pump(250);
        Save(window, outDir, "edge-selected", form, themeId);

        window.Close();
    }

    static void Save(Window window, string outDir, string view, UxFormFactor form, string themeId)
    {
        var path = Path.Combine(outDir, UxCaptureNames.File("readaloud", view, form.Id, themeId));
        window.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window)
            ?? throw new InvalidOperationException($"No frame for {path}");
        frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Console.WriteLine($"Wrote {Path.GetFileName(path)}");
    }

    sealed class SilentAudioPlayer : IAudioPlayer
    {
        public Task PlayAsync(byte[] mp3, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Stop()
        {
        }
    }

    sealed class SilentVoice : IVoiceService
    {
        public Task SpeakAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteToFileAsync(
            string text,
            FileInfo destination,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    sealed class MemoryTokenStore : ISecureTokenStore
    {
        readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
