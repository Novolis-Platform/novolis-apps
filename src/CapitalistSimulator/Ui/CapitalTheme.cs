using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CapitalistSimulator.Ui;

internal static class CapitalPalette
{
    public static readonly Color Window = Color.Parse("#0b1219");
    public static readonly Color Panel = Color.Parse("#14202c");
    public static readonly Color PanelRaised = Color.Parse("#1c2c3c");
    public static readonly Color Accent = Color.Parse("#c8922a");
    public static readonly Color AccentSoft = Color.Parse("#b8a060");
    public static readonly Color Body = Color.Parse("#e6e2d6");
    public static readonly Color Muted = Color.Parse("#8a96a4");
    public static readonly Color Success = Color.Parse("#6ecf8e");
    public static readonly Color Danger = Color.Parse("#c45c4a");
    public static readonly Color MapField = Color.Parse("#0d1822");
    public static readonly Color Road = Color.Parse("#2a3544");
    public static readonly Color Seaport = Color.Parse("#1a4060");
    public static readonly Color Bank = Color.Parse("#3a3420");
    public static readonly Color PlayerFirm = Color.Parse("#2a5a4a");
    public static readonly Color AiFirm = Color.Parse("#5a2a3a");

    public static readonly IBrush WindowBrush = new SolidColorBrush(Window);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush PanelRaisedBrush = new SolidColorBrush(PanelRaised);
    public static readonly IBrush AccentBrush = new SolidColorBrush(Accent);
    public static readonly IBrush BodyBrush = new SolidColorBrush(Body);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush SuccessBrush = new SolidColorBrush(Success);
    public static readonly IBrush DangerBrush = new SolidColorBrush(Danger);
    public static readonly IBrush MapFieldBrush = new SolidColorBrush(MapField);

    public static readonly FontFamily DisplayFont =
        new("Georgia, Palatino Linotype, Book Antiqua, Times New Roman, serif");
    public static readonly FontFamily BodyFont =
        new("Segoe UI, Candara, Calibri, sans-serif");
    public static readonly FontFamily MonoFont =
        new("Consolas, Cascadia Mono, Courier New, monospace");
}

internal enum CapitalButtonKind
{
    Primary,
    Secondary,
    Danger,
    Quiet,
}

internal static class CapitalTheme
{
    public static void ApplyWindowChrome(Window window)
    {
        window.Background = CapitalPalette.WindowBrush;
        window.FontFamily = CapitalPalette.BodyFont;
        window.Foreground = CapitalPalette.BodyBrush;
    }

    public static Button MakeButton(string text, CapitalButtonKind kind = CapitalButtonKind.Secondary)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 0, 6, 4),
            FontFamily = CapitalPalette.BodyFont,
            FontSize = kind == CapitalButtonKind.Primary ? 13 : 12,
            FontWeight = kind == CapitalButtonKind.Primary ? FontWeight.SemiBold : FontWeight.Normal,
            CornerRadius = new CornerRadius(3),
        };
        switch (kind)
        {
            case CapitalButtonKind.Primary:
                btn.Background = CapitalPalette.AccentBrush;
                btn.Foreground = new SolidColorBrush(Color.Parse("#1a1810"));
                break;
            case CapitalButtonKind.Danger:
                btn.Background = new SolidColorBrush(Color.Parse("#3a2220"));
                btn.Foreground = CapitalPalette.DangerBrush;
                break;
            case CapitalButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                btn.Foreground = CapitalPalette.MutedBrush;
                break;
            default:
                btn.Background = CapitalPalette.PanelRaisedBrush;
                btn.Foreground = CapitalPalette.BodyBrush;
                break;
        }
        return btn;
    }

    public static TextBlock Title(string text, double size = 22) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = CapitalPalette.AccentBrush,
    };

    public static TextBlock Label(string text, bool muted = false) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.BodyFont,
        FontSize = 12,
        Foreground = muted ? CapitalPalette.MutedBrush : CapitalPalette.BodyBrush,
        Margin = new Thickness(0, 0, 0, 4),
    };

    public static TextBlock Mono(string text, double size = 12) => new()
    {
        Text = text,
        FontFamily = CapitalPalette.MonoFont,
        FontSize = size,
        Foreground = CapitalPalette.BodyBrush,
    };

    public static Border Section(string title, Control child) => new()
    {
        Background = CapitalPalette.PanelBrush,
        BorderBrush = CapitalPalette.PanelRaisedBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10),
        Margin = new Thickness(0, 0, 0, 8),
        Child = new StackPanel
        {
            Children =
            {
                Label(title, muted: true),
                child,
            },
        },
    };

    public static Border MetricChip(string label, string value) => new()
    {
        Background = CapitalPalette.PanelRaisedBrush,
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(10, 6),
        Margin = new Thickness(0, 0, 8, 4),
        Child = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = CapitalPalette.MutedBrush,
                    FontFamily = CapitalPalette.BodyFont,
                },
                new TextBlock
                {
                    Text = value,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = CapitalPalette.BodyBrush,
                    FontFamily = CapitalPalette.MonoFont,
                },
            },
        },
    };
}
