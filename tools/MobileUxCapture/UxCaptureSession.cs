using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Novolis.Avalonia.GraphicalProfile;

namespace MobileUxCapture;

internal static class UxCaptureSession
{
    public static void ForEachThemeAndForm(
        string outDir,
        string app,
        string view,
        Func<UxFormFactor, ThemeVariant, Control> build,
        Action<string>? onWrote = null,
        int settleMs = 120)
    {
        foreach (var form in UxFormFactor.All)
        {
            CaptureThemed(outDir, app, view, form, ThemeVariant.Light, build, onWrote, settleMs);
            CaptureThemed(outDir, app, view, form, ThemeVariant.Dark, build, onWrote, settleMs);
        }
    }

    public static void CaptureThemed(
        string outDir,
        string app,
        string view,
        UxFormFactor form,
        ThemeVariant theme,
        Func<UxFormFactor, ThemeVariant, Control> build,
        Action<string>? onWrote = null,
        int settleMs = 120)
    {
        var themeId = theme == ThemeVariant.Dark ? "dark" : "light";
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = theme;

        var content = build(form, theme);
        var path = Path.Combine(outDir, UxCaptureNames.File(app, view, form.Id, themeId));
        CaptureControl(content, form.Width, form.Height, path, settleMs);
        onWrote?.Invoke(path);
    }

    public static void CaptureControl(
        Control content,
        int width,
        int height,
        string path,
        int settleMs = 120)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Background = GraphicalProfile.BackgroundBrush,
            Content = content,
            Title = Path.GetFileNameWithoutExtension(path),
        };
        window.Show();
        Pump(settleMs);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Pump(80);
        using var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window)
            ?? throw new InvalidOperationException($"No frame for {path}");
        // Prefer lossless for UI proof — JPEG crush in the eval canvas was muddy.
        frame.Save(path);
        window.Close();
        Console.WriteLine($"Wrote {Path.GetFileName(path)}");
    }

    public static void Pump(int settleMs)
    {
        var until = Environment.TickCount64 + settleMs;
        while (Environment.TickCount64 < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(16);
        }

        Dispatcher.UIThread.RunJobs();
    }

    public static void ResetTheme()
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Default;
    }
}
