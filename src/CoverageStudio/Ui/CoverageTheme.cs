using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoverageStudio.Ui;

/// <summary>Slate + teal engineering chrome for Coverage Studio (not purple SaaS).</summary>
internal static class CoverageTheme
{
    public static readonly Color Canvas = Color.Parse("#0F1720");
    public static readonly Color Panel = Color.Parse("#17212B");
    public static readonly Color PanelAlt = Color.Parse("#1C2834");
    public static readonly Color Border = Color.Parse("#2A3A4A");
    public static readonly Color Text = Color.Parse("#E6EEF5");
    public static readonly Color Muted = Color.Parse("#8FA3B5");
    public static readonly Color Accent = Color.Parse("#2A9D8F");
    public static readonly Color AccentDim = Color.Parse("#1F7A70");
    public static readonly Color Warn = Color.Parse("#E9A319");
    public static readonly Color Danger = Color.Parse("#E05A4E");
    public static readonly Color Ok = Color.Parse("#5CB87A");

    public static readonly IBrush CanvasBrush = new SolidColorBrush(Canvas);
    public static readonly IBrush PanelBrush = new SolidColorBrush(Panel);
    public static readonly IBrush PanelAltBrush = new SolidColorBrush(PanelAlt);
    public static readonly IBrush BorderBrush = new SolidColorBrush(Border);
    public static readonly IBrush TextBrush = new SolidColorBrush(Text);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush AccentBrush = new SolidColorBrush(Accent);
    public static readonly IBrush WarnBrush = new SolidColorBrush(Warn);
    public static readonly IBrush DangerBrush = new SolidColorBrush(Danger);
    public static readonly IBrush OkBrush = new SolidColorBrush(Ok);

    public static readonly FontFamily BodyFont = new("Segoe UI, Candara, Calibri, sans-serif");
    public static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas, Courier New, monospace");

    public static void ApplyWindowChrome(Window window)
    {
        window.Background = CanvasBrush;
        window.Foreground = TextBrush;
        window.FontFamily = BodyFont;
    }

    public static TextBlock Title(string text, double size = 16) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = TextBrush,
        FontFamily = BodyFont,
    };

    public static TextBlock Label(string text, bool muted = false) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = muted ? MutedBrush : TextBrush,
        FontFamily = BodyFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Mono(string text, double size = 12) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = TextBrush,
        FontFamily = MonoFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Border PanelBox(Control child, Thickness? padding = null) => new()
    {
        Background = PanelBrush,
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = padding ?? new Thickness(8),
        Child = child,
    };

    public static Button MakeButton(string content, bool primary = false)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(12, 6),
            FontFamily = BodyFont,
            FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal,
            Background = primary ? AccentBrush : PanelAltBrush,
            Foreground = TextBrush,
            BorderBrush = primary ? new SolidColorBrush(AccentDim) : BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
        };
        return button;
    }

    public static CheckBox MakeCheck(string content, bool isChecked = false) => new()
    {
        Content = content,
        IsChecked = isChecked,
        Foreground = TextBrush,
        FontFamily = BodyFont,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBox MakeTextBox(string text, double minWidth = 200) => new()
    {
        Text = text,
        MinWidth = minWidth,
        FontFamily = MonoFont,
        FontSize = 12,
        Background = PanelAltBrush,
        Foreground = TextBrush,
        BorderBrush = BorderBrush,
        CaretBrush = AccentBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static NumericUpDown MakeNumeric(decimal value, decimal min, decimal max, double width = 80) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        Width = width,
        FontFamily = MonoFont,
        Background = PanelAltBrush,
        Foreground = TextBrush,
        BorderBrush = BorderBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
