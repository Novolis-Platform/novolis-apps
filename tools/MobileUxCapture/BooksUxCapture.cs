using System.Reflection;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using BooksMobile;
using BooksMobile.Services;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

namespace MobileUxCapture;

/// <summary>Headless Flip8-aspect captures of Books Mobile against a local books workspace.</summary>
[SupportedOSPlatform("windows")]
internal static class BooksUxCapture
{
    // Galaxy Z Flip8 main panel is 1080×2520. Capture at 50% DIPs so the frame fits
    // desktop tooling while preserving the ~21:9 phone geometry.
    public const int Flip8Width = 540;
    public const int Flip8Height = 1260;

    public static void CaptureAll(string outDir, string booksWorkspace)
    {
        if (!Directory.Exists(booksWorkspace))
            throw new DirectoryNotFoundException(booksWorkspace);

        Environment.SetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE", Path.GetFullPath(booksWorkspace));
        Environment.SetEnvironmentVariable("BOOKSMOBILE_REPO_NAME", "books");
        Environment.SetEnvironmentVariable("BOOKSMOBILE_CONTENT_PREFIX", "src/");

        var storage = Path.Combine(Path.GetTempPath(), "novolis-ux-booksmobile");
        Directory.CreateDirectory(storage);

        var services = new ServiceCollection();
        services.AddNovolisMobileDesktop("BooksMobileUxCapture");
        services.AddSingleton<IAppDataPaths>(_ => new DesktopAppDataPaths("BooksMobileUxCapture"));
        services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
        services.AddSingleton<IAudioPlayer, SilentAudioPlayer>();
        services.AddSingleton<IVoiceService, SilentVoice>();
        services.AddBooksMobileCore();
        using var provider = services.BuildServiceProvider();

        var session = provider.GetRequiredService<BooksMobileSession>();
        session.TryRestoreSessionAsync().GetAwaiter().GetResult();
        if (!session.IsSignedIn)
            throw new InvalidOperationException("Local books workspace did not sign in.");

        var view = provider.GetRequiredService<MainView>();
        var window = new Window
        {
            Width = Flip8Width,
            Height = Flip8Height,
            Background = GraphicalProfile.BackgroundBrush,
            Content = view,
            Title = "Books Mobile UX capture",
        };
        window.Show();
        Pump(800);

        CaptureFrame(window, Path.Combine(outDir, "books-flip8-library.png"));

        var book = FirstBook(session);
        if (book is null)
        {
            Console.WriteLine("No books found in workspace — library capture only.");
            window.Close();
            return;
        }

        Invoke(view, "ShowBook", book, (string?)null);
        Pump(200);
        CaptureFrame(window, Path.Combine(outDir, "books-flip8-book.png"));

        var chapter = PreferProseChapter(book);
        if (chapter is null)
        {
            window.Close();
            return;
        }

        Invoke(view, "OpenChapter", chapter);
        Pump(600);
        CaptureFrame(window, Path.Combine(outDir, "books-flip8-chapter-read.png"));

        Invoke(view, "ToggleChapterMode");
        Pump(400);
        CaptureFrame(window, Path.Combine(outDir, "books-flip8-chapter-edit.png"));

        Invoke(view, "ShowReadAnything");
        Pump(200);
        CaptureFrame(window, Path.Combine(outDir, "books-flip8-read-anything.png"));

        // Auth chrome without local workspace (separate short-lived session).
        CaptureAuth(outDir);

        window.Close();
    }

    static void CaptureAuth(string outDir)
    {
        Environment.SetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE", null);
        var services = new ServiceCollection();
        services.AddNovolisMobileDesktop("BooksMobileUxCaptureAuth");
        services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
        services.AddSingleton<IAudioPlayer, SilentAudioPlayer>();
        services.AddSingleton<IVoiceService, SilentVoice>();
        services.AddBooksMobileCore();
        using var provider = services.BuildServiceProvider();
        var view = provider.GetRequiredService<MainView>();
        var window = new Window
        {
            Width = Flip8Width,
            Height = Flip8Height,
            Background = GraphicalProfile.BackgroundBrush,
            Content = view,
        };
        window.Show();
        Pump(600);
        CaptureFrame(window, Path.Combine(outDir, "books-flip8-auth.png"));
        window.Close();
    }

    static BookInfo? FirstBook(BooksMobileSession session)
    {
        // Prefer a multi-chapter book with real prose for read-mode UX evidence.
        BookInfo? fallback = null;
        foreach (var series in session.LoadSeries())
        {
            foreach (var book in series.Books.Where(b => b.Chapters.Count > 0))
            {
                fallback ??= book;
                if (book.Chapters.Count >= 5
                    && book.Title.Contains("Calypso", StringComparison.OrdinalIgnoreCase))
                    return book;
                if (book.Chapters.Count >= 5)
                    return book;
            }
        }

        return fallback
            ?? session.LoadStandaloneBooks().FirstOrDefault(b => b.Chapters.Count > 0);
    }

    static ChapterInfo? PreferProseChapter(BookInfo book)
    {
        foreach (var chapter in book.Chapters.Take(12))
        {
            if (!File.Exists(chapter.FilePath))
                continue;
            var text = File.ReadAllText(chapter.FilePath);
            if (text.Length > 400 && text.Contains('\n'))
                return chapter;
        }

        return book.Chapters.FirstOrDefault();
    }

    static void Invoke(object target, string method, params object?[] args)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var candidates = target.GetType().GetMethods(flags).Where(m => m.Name == method).ToArray();
        var mi = candidates.FirstOrDefault(m => m.GetParameters().Length == args.Length)
            ?? throw new MissingMethodException(target.GetType().FullName, method);
        mi.Invoke(target, args);
    }

    static void Pump(int settleMs)
    {
        var until = Environment.TickCount64 + settleMs;
        while (Environment.TickCount64 < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(16);
        }

        Dispatcher.UIThread.RunJobs();
    }

    static void CaptureFrame(Window window, string path)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"No frame for {path}");
        frame.Save(path, PngBitmapEncoderOptions.Default);
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
}
