using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Navigation;
using Novolis.Commands.Navigation;
using SkiaSharp;
using Sudoku.App;

namespace MobileUxCapture;

internal static class Program
{
    public static int Main(string[] args)
    {
        var outDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", ".tmp", "ux-captures"));
        Directory.CreateDirectory(outDir);

        CapturePdfWireframes(outDir);

        AppBuilder.Configure<CaptureApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            })
            .SetupWithoutStarting();

        CaptureSudoku(outDir, 390, 844, "sudoku-phone.png");
        CaptureSudoku(outDir, 1280, 800, "sudoku-desktop.png");
        CaptureShell(outDir, 390, 844, "presence-shell-phone.png");
        CaptureShell(outDir, 1280, 800, "presence-shell-desktop.png");
        CapturePdfWelcome(outDir, 390, 844, "pdf-welcome-phone.png");

        Console.WriteLine($"Wrote UX captures to {outDir}");
        foreach (var file in Directory.EnumerateFiles(outDir, "*.png").OrderBy(static path => path))
            Console.WriteLine(Path.GetFileName(file));
        return 0;
    }

    static void CaptureSudoku(string outDir, int width, int height, string fileName)
    {
        var storage = Path.Combine(Path.GetTempPath(), "novolis-ux-sudoku");
        Directory.CreateDirectory(storage);
        var services = new ServiceCollection()
            .AddSudokuApp(storage)
            .BuildServiceProvider();
        var view = new SudokuView(services.GetRequiredService<SudokuGameService>());
        CaptureControl(view, width, height, Path.Combine(outDir, fileName));
    }

    static void CaptureShell(string outDir, int width, int height, string fileName)
    {
        var tree = new MobileMenuTree(
        [
            new MobileMenuNode("today", "Today")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "today",
            },
            new MobileMenuNode("map", "Map")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "map",
            },
            new MobileMenuNode("places", "Places")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "places",
            },
            new MobileMenuNode("report", "Report")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "report",
            },
            new MobileMenuNode("settings", "Settings", MobileMenuNodeKind.Action)
            {
                CommandId = "settings",
            },
        ]);
        var body = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "YOUR DAY",
                        Classes = { "eyebrow" },
                        Foreground = GraphicalProfile.AccentBrush,
                    },
                    new TextBlock
                    {
                        Text = "Presence Ledger",
                        FontSize = 28,
                        FontWeight = FontWeight.Bold,
                        Foreground = GraphicalProfile.TextBrush,
                    },
                    new TextBlock
                    {
                        Text = "Primary tabs stay single-line; body fills the remaining height.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = GraphicalProfile.MutedBrush,
                    },
                    new Border
                    {
                        Background = GraphicalProfile.RaisedBrush,
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14),
                        Child = new TextBlock
                        {
                            Text = "Observation · live · 12 samples",
                            Foreground = GraphicalProfile.TextBrush,
                        },
                    },
                },
            },
        };
        var shell = new MobileNavigationShell(new MobileNavigationController(tree))
        {
            Title = "Presence Ledger",
            Body = body,
        };
        CaptureControl(shell, width, height, Path.Combine(outDir, fileName));
    }

    static void CapturePdfWelcome(string outDir, int width, int height, string fileName)
    {
        var welcome = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18),
            Margin = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "OFFLINE PDF READER",
                        Foreground = GraphicalProfile.AccentBrush,
                        FontWeight = FontWeight.Bold,
                    },
                    new TextBlock
                    {
                        Text = "Read without sending your files away.",
                        FontSize = 24,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = GraphicalProfile.TextBrush,
                    },
                    new TextBlock
                    {
                        Text = "Welcome fills the body on phone widths instead of an empty viewer strip.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = GraphicalProfile.MutedBrush,
                    },
                    new Button
                    {
                        Content = "Open a PDF document",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        MinHeight = 44,
                        Background = GraphicalProfile.AccentFillBrush,
                        Foreground = GraphicalProfile.OnAccentFillBrush,
                    },
                },
            },
        };
        var tree = new MobileMenuTree(
        [
            new MobileMenuNode("pdf.open", "Open PDF", MobileMenuNodeKind.Action)
            {
                CommandId = "pdf.open",
            },
        ]);
        var shell = new MobileNavigationShell(new MobileNavigationController(tree))
        {
            Title = "PDF Reader",
            Body = welcome,
        };
        CaptureControl(shell, width, height, Path.Combine(outDir, fileName));
    }

    static void CaptureControl(Control content, int width, int height, string path)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Background = GraphicalProfile.BackgroundBrush,
            Content = content,
        };
        window.Show();
        window.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"No frame for {path}");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        window.Close();
    }

    static void CapturePdfWireframes(string outDir)
    {
        DrawPhoneWireframe(
            Path.Combine(outDir, "pdf-before-sliver.png"),
            title: "BEFORE · empty compact viewer",
            bodyFill: false);
        DrawPhoneWireframe(
            Path.Combine(outDir, "pdf-after-welcome.png"),
            title: "AFTER · welcome fills body",
            bodyFill: true);
    }

    static void DrawPhoneWireframe(string path, string title, bool bodyFill)
    {
        const int width = 390;
        const int height = 844;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x0B, 0x1F, 0x2A));

        using var headerPaint = new SKPaint { Color = new SKColor(0x12, 0x2B, 0x3A), IsAntialias = true };
        using var accentPaint = new SKPaint { Color = new SKColor(0x3D, 0xB8, 0xA0), IsAntialias = true };
        using var mutedPaint = new SKPaint { Color = new SKColor(0x8A, 0xA0, 0xAD), IsAntialias = true };
        using var surfacePaint = new SKPaint { Color = new SKColor(0x16, 0x33, 0x44), IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var smallPaint = new SKPaint { Color = new SKColor(0xC7, 0xD6, 0xDE), IsAntialias = true };
        using var boldFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);

        void Draw(string text, float x, float y, SKFont font, SKPaint paint) =>
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);

        canvas.DrawRect(0, 0, width, 56, headerPaint);
        Draw("PDF Reader", 16, 36, boldFont, textPaint);
        Draw(title, 16, 84, smallFont, smallPaint);

        if (bodyFill)
        {
            canvas.DrawRoundRect(16, 104, width - 32, height - 180, 16, 16, surfacePaint);
            Draw("Welcome / document body", 32, 140, boldFont, textPaint);
            Draw("Open · search · read locally", 32, 168, smallFont, smallPaint);
            canvas.DrawRoundRect(32, 200, width - 64, 48, 10, 10, accentPaint);
            Draw("Open a PDF document", 56, 230, boldFont, textPaint);
        }
        else
        {
            canvas.DrawRect(0, 104, width, 44, surfacePaint);
            Draw("Viewer header", 16, 132, smallFont, smallPaint);
            canvas.DrawRect(0, 148, width, 40, headerPaint);
            Draw("Toolbar", 16, 174, smallFont, smallPaint);
            canvas.DrawRect(0, 188, width, 8, accentPaint);
            Draw("Star workspace collapsed to sliver", 16, 220, smallFont, mutedPaint);
        }

        canvas.DrawRect(0, height - 56, width, 56, headerPaint);
        Draw("More", 16, height - 24, smallFont, smallPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        data.SaveTo(stream);
    }
}
