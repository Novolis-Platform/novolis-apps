using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace BooksMobile.Ui;

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
        root.Background = GraphicalProfile.BackgroundBrush;

    public static TextBlock BrandTitle(string text, double size = 28) => new()
    {
        Text = text,
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = GraphicalProfile.AccentBrush,
    };

    public static TextBlock BrandWordmark() => new()
    {
        Text = "NOVOLIS",
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = 13,
        FontWeight = FontWeight.Bold,
        LetterSpacing = 3,
        Foreground = GraphicalProfile.AccentBrush,
        Opacity = 0.95,
    };

    public static TextBlock Muted(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = size,
        Foreground = GraphicalProfile.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Body(string text, double size = 15) => new()
    {
        Text = text,
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = size,
        Foreground = GraphicalProfile.TextBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static Button Button(string text, BooksButtonKind kind = BooksButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = GraphicalProfile.BodyFont,
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
                btn.Background = GraphicalProfile.AccentFillBrush;
                btn.Foreground = GraphicalProfile.BackgroundBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            case BooksButtonKind.Danger:
                btn.Background = GraphicalProfile.RaisedBrush;
                btn.Foreground = GraphicalProfile.DangerBrush;
                btn.BorderBrush = GraphicalProfile.DangerBrush;
                btn.BorderThickness = new Thickness(1);
                break;
            case BooksButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = GraphicalProfile.MutedBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            default:
                btn.Background = GraphicalProfile.RaisedBrush;
                btn.Foreground = GraphicalProfile.TextBrush;
                btn.BorderBrush = GraphicalProfile.AccentBrush;
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 47, 223, 255));
                break;
        }
    }

    public static Border Card(Control child) => new()
    {
        Background = GraphicalProfile.SurfaceBrush,
        BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(16, 14),
        Margin = new Thickness(0, 0, 0, 10),
        Child = child,
    };
}
