using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SpaceFleetSurveyTeam.Ui;

/// <summary>
/// Field-instrument tokens — deep navy / teal canvas with copper amber accent.
/// Not Inter, not purple SaaS, not cream+terracotta broadsheet.
/// </summary>
internal static class SurveyPalette
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

internal enum SurveyButtonKind
{
    Primary,
    Secondary,
    Quiet,
}

internal static class SurveyTheme
{
    public static void ApplyRoot(Panel root) =>
        root.Background = SurveyPalette.WindowBrush;

    public static TextBlock BrandTitle(string text, double size = 36) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.DisplayFont,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        Foreground = SurveyPalette.BodyBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Tagline(string text) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.BodyFont,
        FontSize = 16,
        Foreground = SurveyPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 520,
        Margin = new Avalonia.Thickness(0, 8, 0, 0),
    };

    public static TextBlock Muted(string text, double size = 13) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.BodyFont,
        FontSize = size,
        Foreground = SurveyPalette.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Label(string text, IBrush? foreground = null) => new()
    {
        Text = text,
        FontFamily = SurveyPalette.DisplayFont,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 1.2,
        Foreground = foreground ?? SurveyPalette.TealBrush,
    };

    public static Button Button(string text, SurveyButtonKind kind)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = SurveyPalette.BodyFont,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Padding = new Avalonia.Thickness(20, 12),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        switch (kind)
        {
            case SurveyButtonKind.Primary:
                button.Background = SurveyPalette.AmberBrush;
                button.Foreground = SurveyPalette.WindowBrush;
                break;
            case SurveyButtonKind.Secondary:
                button.Background = SurveyPalette.PanelRaisedBrush;
                button.Foreground = SurveyPalette.TealBrush;
                break;
            default:
                button.Background = Avalonia.Media.Brushes.Transparent;
                button.Foreground = SurveyPalette.MutedBrush;
                break;
        }

        return button;
    }
}
