using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ReadAloud.Ui;

/// <summary>Novolis brand tokens for Read Aloud — navy + cyan, reading-first (not Inter / purple SaaS).</summary>
internal static class ReadAloudPalette
{
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

internal enum ReadAloudButtonKind
{
    Primary,
    Secondary,
    Quiet,
    Danger,
}

internal static class ReadAloudTheme
{
    public static TextBlock BrandTitle(string text, double size = 28) => new()
    {
        Text = text,
        FontFamily = ReadAloudPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = ReadAloudPalette.AccentBrush,
    };

    public static TextBlock Muted(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = ReadAloudPalette.BodyFont,
        FontSize = size,
        Foreground = ReadAloudPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static Button Button(string text, ReadAloudButtonKind kind = ReadAloudButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = ReadAloudPalette.BodyFont,
            FontSize = kind == ReadAloudButtonKind.Primary ? 15 : 14,
            FontWeight = kind == ReadAloudButtonKind.Primary ? FontWeight.SemiBold : FontWeight.Normal,
            Padding = new Thickness(16, 10),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 0,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        StyleButton(btn, kind);
        return btn;
    }

    public static void StyleButton(Button btn, ReadAloudButtonKind kind)
    {
        switch (kind)
        {
            case ReadAloudButtonKind.Primary:
                btn.Background = ReadAloudPalette.AccentDeepBrush;
                btn.Foreground = ReadAloudPalette.WindowBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            case ReadAloudButtonKind.Danger:
                btn.Background = ReadAloudPalette.PanelRaisedBrush;
                btn.Foreground = ReadAloudPalette.DangerBrush;
                btn.BorderBrush = ReadAloudPalette.DangerBrush;
                btn.BorderThickness = new Thickness(1);
                break;
            case ReadAloudButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = ReadAloudPalette.MutedBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            default:
                btn.Background = ReadAloudPalette.PanelRaisedBrush;
                btn.Foreground = ReadAloudPalette.BodyBrush;
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 47, 223, 255));
                break;
        }
    }
}
