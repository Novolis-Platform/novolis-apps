using System.Reflection;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using BooksMobile;
using BooksMobile.Services;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Audio.Voice;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Mobile.Desktop;
using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

namespace MobileUxCapture;

/// <summary>
/// Books Mobile UX matrix: every primary view × phone/desktop × light/dark.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class BooksUxCapture
{
    public static void CaptureAll(string outDir, string booksWorkspace)
    {
        if (!Directory.Exists(booksWorkspace))
            throw new DirectoryNotFoundException(booksWorkspace);

        foreach (var form in UxFormFactor.All)
        {
            CaptureAuthMatrix(outDir, form);
            CaptureSignedInJourney(outDir, booksWorkspace, form);
        }

        UxCaptureSession.ResetTheme();
    }

    static void CaptureAuthMatrix(string outDir, UxFormFactor form)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Environment.SetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE", null);
            Application.Current!.RequestedThemeVariant = theme;
            var view = CreateView("BooksMobileUxAuth");
            UxCaptureSession.CaptureControl(
                view,
                form.Width,
                form.Height,
                Path.Combine(outDir, UxCaptureNames.File("books", "auth", form.Id, ThemeId(theme))));
        }
    }

    static void CaptureSignedInJourney(string outDir, string booksWorkspace, UxFormFactor form)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Environment.SetEnvironmentVariable("BOOKSMOBILE_LOCAL_WORKSPACE", Path.GetFullPath(booksWorkspace));
            Environment.SetEnvironmentVariable("BOOKSMOBILE_REPO_NAME", "books");
            Environment.SetEnvironmentVariable("BOOKSMOBILE_CONTENT_PREFIX", "src/");
            Application.Current!.RequestedThemeVariant = theme;

            using var provider = BuildProvider("BooksMobileUxJourney");
            var session = provider.GetRequiredService<BooksMobileSession>();
            session.TryRestoreSessionAsync().GetAwaiter().GetResult();
            if (!session.IsSignedIn)
                throw new InvalidOperationException("Local books workspace did not sign in.");

            var view = provider.GetRequiredService<MainView>();
            var window = new Window
            {
                Width = form.Width,
                Height = form.Height,
                Background = GraphicalProfile.BackgroundBrush,
                Content = view,
                Title = $"Books {form.Id} {ThemeId(theme)}",
            };
            window.Show();
            UxCaptureSession.Pump(700);

            Save(window, outDir, "library", form, theme);

            var book = FirstBook(session);
            if (book is null)
            {
                window.Close();
                continue;
            }

            Invoke(view, "ShowBook", book, (string?)null);
            UxCaptureSession.Pump(250);
            Save(window, outDir, "book", form, theme);

            var chapter = PreferProseChapter(book);
            if (chapter is null)
            {
                window.Close();
                continue;
            }

            Invoke(view, "OpenChapter", chapter);
            UxCaptureSession.Pump(500);
            Save(window, outDir, "chapter-read", form, theme);

            Invoke(view, "ToggleChapterMode");
            UxCaptureSession.Pump(350);
            Save(window, outDir, "chapter-edit", form, theme);

            // Keyboard squish is phone-only proof (desktop rarely shows IME height).
            if (form.Id == UxFormFactor.Phone.Id)
            {
                PlaceCaretNearEnd(view);
                view.SimulateSoftKeyboard(420);
                UxCaptureSession.Pump(400);
                Save(window, outDir, "chapter-edit-keyboard", form, theme);
                Console.WriteLine($"SoftKeyboardBottom={view.SoftKeyboardBottom} ({ThemeId(theme)})");
                view.SimulateSoftKeyboard(0);
                UxCaptureSession.Pump(150);
            }

            Invoke(view, "ShowReadAnything");
            UxCaptureSession.Pump(250);
            Save(window, outDir, "read-anything", form, theme);

            window.Close();
        }
    }

    static void Save(Window window, string outDir, string view, UxFormFactor form, ThemeVariant theme)
    {
        var path = Path.Combine(outDir, UxCaptureNames.File("books", view, form.Id, ThemeId(theme)));
        window.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window)
            ?? throw new InvalidOperationException($"No frame for {path}");
        frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Console.WriteLine($"Wrote {Path.GetFileName(path)}");
    }

    static MainView CreateView(string profile)
    {
        using var provider = BuildProvider(profile);
        return provider.GetRequiredService<MainView>();
    }

    static ServiceProvider BuildProvider(string profile)
    {
        var services = new ServiceCollection();
        services.AddNovolisMobileDesktop(profile);
        services.AddSingleton<IAppDataPaths>(_ => new DesktopAppDataPaths(profile));
        services.AddSingleton<IScreenWakeLock, NullScreenWakeLock>();
        services.AddSingleton<IAudioPlayer, SilentAudioPlayer>();
        services.AddSingleton<IVoiceService, SilentVoice>();
        services.AddBooksMobileCore();
        return services.BuildServiceProvider();
    }

    static string ThemeId(ThemeVariant theme) =>
        theme == ThemeVariant.Dark ? "dark" : "light";

    static void PlaceCaretNearEnd(MainView view)
    {
        var editorField = typeof(MainView).GetField(
            "_editor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (editorField?.GetValue(view) is not MarkdownSourceEditor editor)
            return;
        var text = editor.Text ?? string.Empty;
        editor.MoveCaretTo(Math.Max(0, text.Length - 1));
        editor.EnsureCaretVisible();
    }

    static BookInfo? FirstBook(BooksMobileSession session)
    {
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
