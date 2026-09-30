using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace ReadAloud.Ui;

internal static class ReadAloudTheme
{
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

    public static TextBox Field(string? placeholder = null, bool secret = false)
    {
        var box = new TextBox
        {
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 14,
            PlaceholderText = placeholder,
            PasswordChar = secret ? '•' : default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        GraphicalProfileBinding.Bind(
            box,
            TextBox.ForegroundProperty,
            GraphicalProfile.TextResourceKey);
        GraphicalProfileBinding.Bind(
            box,
            TextBox.BackgroundProperty,
            GraphicalProfile.RaisedResourceKey);
        GraphicalProfileBinding.Bind(
            box,
            TextBox.CaretBrushProperty,
            GraphicalProfile.AccentResourceKey);
        return box;
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
            case ReadAloudButtonKind.Danger:
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
            case ReadAloudButtonKind.Quiet:
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
}
