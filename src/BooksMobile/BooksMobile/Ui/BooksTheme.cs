using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace BooksMobile.Ui;

/// <summary>Novolis brand tokens for Books Mobile — navy + cyan, reading-first (not Inter / purple SaaS).</summary>
internal static class BooksPalette
{
    public static Color Window => GraphicalProfile.Background;
    public static Color Panel => GraphicalProfile.Surface;
    public static Color PanelRaised => GraphicalProfile.Raised;
    public static Color Accent => GraphicalProfile.Accent;
    public static Color AccentDeep => GraphicalProfile.AccentFill;
    public static Color Body => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Danger => GraphicalProfile.Danger;

    public static IBrush WindowBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush PanelBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush PanelRaisedBrush => GraphicalProfile.RaisedBrush;
    public static IBrush AccentBrush => GraphicalProfile.AccentBrush;
    public static IBrush AccentDeepBrush => GraphicalProfile.AccentFillBrush;
    public static IBrush BodyBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush DangerBrush => GraphicalProfile.DangerBrush;

    public static FontFamily DisplayFont => GraphicalProfile.BodyFont;
    public static FontFamily BodyFont => GraphicalProfile.BodyFont;
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
