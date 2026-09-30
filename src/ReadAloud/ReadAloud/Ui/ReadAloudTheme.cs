using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace ReadAloud.Ui;

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
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = GraphicalProfile.AccentBrush,
    };

    public static TextBlock Muted(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = GraphicalProfile.BodyFont,
        FontSize = size,
        Foreground = GraphicalProfile.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static Button Button(string text, ReadAloudButtonKind kind = ReadAloudButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = GraphicalProfile.BodyFont,
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
                btn.Background = GraphicalProfile.AccentFillBrush;
                btn.Foreground = GraphicalProfile.BackgroundBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            case ReadAloudButtonKind.Danger:
                btn.Background = GraphicalProfile.RaisedBrush;
                btn.Foreground = GraphicalProfile.DangerBrush;
                btn.BorderBrush = GraphicalProfile.DangerBrush;
                btn.BorderThickness = new Thickness(1);
                break;
            case ReadAloudButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = GraphicalProfile.MutedBrush;
                btn.BorderThickness = new Thickness(0);
                break;
            default:
                btn.Background = GraphicalProfile.RaisedBrush;
                btn.Foreground = GraphicalProfile.TextBrush;
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 47, 223, 255));
                break;
        }
    }
}
