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
    public static void ApplyRoot(Panel root)
    {
        GraphicalProfileBinding.Bind(
            root,
            Panel.BackgroundProperty,
            GraphicalProfile.BackgroundResourceKey);
    }

    public static TextBlock BrandTitle(string text, double size = 28)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
        };
        GraphicalProfileBinding.Bind(
            block,
            TextBlock.ForegroundProperty,
            GraphicalProfile.AccentResourceKey);
        return block;
    }

    public static TextBlock BrandWordmark()
    {
        var block = new TextBlock
        {
            Text = "NOVOLIS",
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 3,
            Opacity = 0.95,
        };
        GraphicalProfileBinding.Bind(
            block,
            TextBlock.ForegroundProperty,
            GraphicalProfile.AccentResourceKey);
        return block;
    }

    public static TextBlock Muted(string text, double size = 14)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        GraphicalProfileBinding.Bind(
            block,
            TextBlock.ForegroundProperty,
            GraphicalProfile.MutedResourceKey);
        return block;
    }

    public static TextBlock Body(string text, double size = 15)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        GraphicalProfileBinding.Bind(
            block,
            TextBlock.ForegroundProperty,
            GraphicalProfile.TextResourceKey);
        return block;
    }

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
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.BackgroundProperty,
                    GraphicalProfile.AccentFillResourceKey);
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.ForegroundProperty,
                    GraphicalProfile.OnAccentFillResourceKey);
                btn.BorderThickness = new Thickness(0);
                break;
            case BooksButtonKind.Danger:
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.BackgroundProperty,
                    GraphicalProfile.RaisedResourceKey);
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.ForegroundProperty,
                    GraphicalProfile.DangerResourceKey);
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.BorderBrushProperty,
                    GraphicalProfile.DangerResourceKey);
                btn.BorderThickness = new Thickness(1);
                break;
            case BooksButtonKind.Quiet:
                btn.Background = Brushes.Transparent;
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.ForegroundProperty,
                    GraphicalProfile.MutedResourceKey);
                btn.BorderThickness = new Thickness(0);
                break;
            default:
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.BackgroundProperty,
                    GraphicalProfile.RaisedResourceKey);
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.ForegroundProperty,
                    GraphicalProfile.TextResourceKey);
                GraphicalProfileBinding.Bind(
                    btn,
                    Avalonia.Controls.Button.BorderBrushProperty,
                    GraphicalProfile.BorderResourceKey);
                btn.BorderThickness = new Thickness(1);
                break;
        }
    }

    public static Border Card(Control child)
    {
        var card = new Border
        {
            BorderThickness = new Thickness(GraphicalProfileColors.Stroke),
            CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
            Padding = new Thickness(GraphicalProfileColors.CardPadding),
            Margin = new Thickness(0, 0, 0, 10),
            Child = child,
        };
        GraphicalProfileBinding.Bind(
            card,
            Border.BackgroundProperty,
            GraphicalProfile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(
            card,
            Border.BorderBrushProperty,
            GraphicalProfile.BorderResourceKey);
        return card;
    }
}
