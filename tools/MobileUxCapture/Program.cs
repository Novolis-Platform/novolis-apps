using Avalonia;
using Avalonia.Headless;

namespace MobileUxCapture;

internal static class Program
{
    public static int Main(string[] args)
    {
        var positional = args.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var outDir = positional.Length > 0
            ? Path.GetFullPath(positional[0])
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", ".tmp", "ux-captures"));
        Directory.CreateDirectory(outDir);

        var booksOnly = args.Any(static a =>
            string.Equals(a, "--books", StringComparison.OrdinalIgnoreCase));
        var catalogOnly = args.Any(static a =>
            string.Equals(a, "--catalog", StringComparison.OrdinalIgnoreCase));
        var readAloudOnly = args.Any(static a =>
            string.Equals(a, "--readaloud", StringComparison.OrdinalIgnoreCase));
        var booksWorkspace = args
            .SkipWhile(static a => !string.Equals(a, "--books-workspace", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault(static a => !a.StartsWith("--", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(booksWorkspace))
            booksWorkspace = @"d:\novolis\.tmp\books-ux-eval";

        AppBuilder.Configure<CaptureApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            })
            .SetupWithoutStarting();

        if (readAloudOnly)
        {
            ReadAloudUxCapture.CaptureAll(outDir);
        }
        else
        {
            if (!booksOnly)
            {
                ShellUxCapture.CaptureSudoku(outDir);
                ShellUxCapture.CapturePresence(outDir);
                ShellUxCapture.CapturePdf(outDir);
                ShellUxCapture.CaptureAppCatalog(outDir);
                ReadAloudUxCapture.CaptureAll(outDir);
            }
            else if (catalogOnly)
            {
                ShellUxCapture.CaptureAppCatalog(outDir);
            }

            if (!catalogOnly)
            {
                if (Directory.Exists(booksWorkspace))
                    BooksUxCapture.CaptureAll(outDir, booksWorkspace);
                else
                    Console.WriteLine($"Skip Books captures — workspace missing: {booksWorkspace}");
            }
        }

        UxCaptureSession.ResetTheme();

        Console.WriteLine($"Wrote UX captures to {outDir}");
        foreach (var file in Directory.EnumerateFiles(outDir, "*.png").OrderBy(static path => path))
            Console.WriteLine(Path.GetFileName(file));
        return 0;
    }
}
