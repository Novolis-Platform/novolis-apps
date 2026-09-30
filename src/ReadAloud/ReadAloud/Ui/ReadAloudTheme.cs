using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace ReadAloud.Ui;

/// <summary>Novolis brand tokens for Read Aloud — navy + cyan, reading-first (not Inter / purple SaaS).</summary>
internal static class ReadAloudPalette
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
