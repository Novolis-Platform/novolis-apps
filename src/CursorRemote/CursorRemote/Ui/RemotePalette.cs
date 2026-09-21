using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CursorRemote.Ui;

internal static class RemotePalette
{
    public static readonly Color Window = Color.Parse("#061018");
    public static readonly Color Panel = Color.Parse("#0c1c28");
    public static readonly Color PanelRaised = Color.Parse("#132a38");
    public static readonly Color Teal = Color.Parse("#2bb8a8");
    public static readonly Color Amber = Color.Parse("#d4894a");
    public static readonly Color Body = Color.Parse("#e6eef4");
    public static readonly Color Muted = Color.Parse("#8aa0b0");
    public static readonly Color Uncertain = Color.Parse("#4a5560");

    public static readonly IBrush WindowBrush = new SolidColorBrush(Window);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush PanelRaisedBrush = new SolidColorBrush(PanelRaised);
    public static readonly IBrush TealBrush = new SolidColorBrush(Teal);
    public static readonly IBrush AmberBrush = new SolidColorBrush(Amber);
    public static readonly IBrush BodyBrush = new SolidColorBrush(Body);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush UncertainBrush = new SolidColorBrush(Uncertain);

    public static readonly FontFamily DisplayFont =
        new("Bahnschrift, Segoe UI Semibold, Candara, sans-serif");

    public static readonly FontFamily BodyFont =
        new("Segoe UI, Candara, Calibri, sans-serif");

    public static readonly FontFamily MonoFont =
        new("Cascadia Mono, Consolas, Courier New, monospace");
}

internal enum RemoteButtonKind
{
    Primary,
    Secondary,
    Quiet,
}

internal static class RemoteTheme
{
    public static TextBlock Title(string text, double size = 32) => new()
    {
        Text = text,
        FontFamily = RemotePalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = RemotePalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Body(string text, double size = 14) => new()
    {
        Text = text,
        FontFamily = RemotePalette.BodyFont,
        FontSize = size,
        Foreground = RemotePalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Muted(string text, double size = 13) => new()
    {
        Text = text,
        FontFamily = RemotePalette.BodyFont,
        FontSize = size,
        Foreground = RemotePalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Label(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = RemotePalette.DisplayFont,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 1.2,
        Foreground = RemotePalette.TealBrush,
    };

    public static Button Button(string text, RemoteButtonKind kind) =>
        new()
        {
            Content = text,
            FontFamily = RemotePalette.BodyFont,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Padding = new Avalonia.Thickness(16, 10),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Background = kind switch
            {
                RemoteButtonKind.Primary => RemotePalette.AmberBrush,
                RemoteButtonKind.Secondary => RemotePalette.PanelRaisedBrush,
                _ => Brushes.Transparent,
            },
            Foreground = kind switch
            {
                RemoteButtonKind.Primary => RemotePalette.WindowBrush,
                RemoteButtonKind.Secondary => RemotePalette.TealBrush,
                _ => RemotePalette.MutedBrush,
            },
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    public static TextBox TextBox(string? watermark = null) =>
        new()
        {
            FontFamily = RemotePalette.BodyFont,
            FontSize = 14,
            Padding = new Avalonia.Thickness(10, 8),
            Background = RemotePalette.PanelRaisedBrush,
            Foreground = RemotePalette.BodyBrush,
            BorderBrush = RemotePalette.UncertainBrush,
            PlaceholderText = watermark,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
}
