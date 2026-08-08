using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace BooksMobile.Ui;

/// <summary>Novolis brand tokens for Books Mobile — navy + cyan, reading-first (not Inter / purple SaaS).</summary>
internal static class BooksPalette
{
    // Org brand canvas + mark cyan → blue (.github/brand).
    public static readonly Color Window = Color.Parse("#05070d");
    public static readonly Color Panel = Color.Parse("#0b1522");
    public static readonly Color PanelRaised = Color.Parse("#122033");
    public static readonly Color Accent = Color.Parse("#2fdfff");
    public static readonly Color AccentDeep = Color.Parse("#0997ff");
    public static readonly Color Body = Color.Parse("#e8eef6");
    public static readonly Color Muted = Color.Parse("#8a9bb0");
    public static readonly Color Danger = Color.Parse("#e07070");

    public static readonly IBrush WindowBrush = new SolidColorBrush(Window);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush PanelRaisedBrush = new SolidColorBrush(PanelRaised);
    public static readonly IBrush AccentBrush = new SolidColorBrush(Accent);
    public static readonly IBrush AccentDeepBrush = new SolidColorBrush(AccentDeep);
    public static readonly IBrush BodyBrush = new SolidColorBrush(Body);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush DangerBrush = new SolidColorBrush(Danger);

    public static readonly FontFamily DisplayFont =
        new("Georgia, Palatino Linotype, Book Antiqua, Times New Roman, serif");

    public static readonly FontFamily BodyFont =
        new("Segoe UI, Candara, Calibri, sans-serif");
}

internal enum BooksButtonKind
{
    Primary,
    Secondary,
    Quiet,
    Danger,
}

internal static class BooksTheme
{
    public static void ApplyRoot(Panel root) =>
        root.Background = BooksPalette.WindowBrush;

    public static TextBlock BrandTitle(string text, double size = 28) => new()
    {
        Text = text,
        FontFamily = BooksPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = BooksPalette.AccentBrush,
    };

    public static TextBlock BrandWordmark() => new()
    {
        Text = "NOVOLIS",
        FontFamily = BooksPalette.DisplayFont,
        FontSize = 13,
        FontWeight = FontWeight.Bold,
        LetterSpacing = 3,
        Foreground = BooksPalette.AccentBrush,
        Opacity = 0.95,
    };

    public static TextBlock Muted(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = BooksPalette.BodyFont,
        FontSize = size,
        Foreground = BooksPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Body(string text, double size = 15) => new()
    {
        Text = text,
        FontFamily = BooksPalette.BodyFont,
        FontSize = size,
        Foreground = BooksPalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static Button Button(string text, BooksButtonKind kind = BooksButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = BooksPalette.BodyFont,
            FontSize = kind == BooksButtonKind.Primary ? 15 : 14,
            FontWeight = kind == BooksButtonKind.Primary ? FontWeight.SemiBold : FontWeight.Normal,
            Padding = new Thickness(16, 10),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        StyleButton(btn, kind);
        return btn;
    }

    public static void StyleButton(Button btn, BooksButtonKind kind)
    {
        switch (kind)
        {
            case BooksButtonKind.Primary:
                btn.Background = BooksPalette.AccentDeepBrush;
                btn.Foreground = BooksPalette.WindowBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            case BooksButtonKind.Danger:
                btn.Background = BooksPalette.PanelRaisedBrush;
                btn.Foreground = BooksPalette.DangerBrush;
                btn.BorderBrush = BooksPalette.DangerBrush;
                btn.BorderThickness = new Thickness(1);
                break;
            case BooksButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = BooksPalette.MutedBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            default:
                btn.Background = BooksPalette.PanelRaisedBrush;
                btn.Foreground = BooksPalette.BodyBrush;
                btn.BorderBrush = BooksPalette.AccentBrush;
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 47, 223, 255));
                break;
        }
    }

    public static Border Card(Control child) => new()
    {
        Background = BooksPalette.PanelBrush,
        BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(16, 14),
        Margin = new Thickness(0, 0, 0, 10),
        Child = child,
    };
}
