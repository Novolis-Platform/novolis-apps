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

    public static TextBlock Heading(string text, double size = 18)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        GraphicalProfileBinding.Bind(
            block,
            TextBlock.ForegroundProperty,
            GraphicalProfile.TextResourceKey);
        return block;
    }

    public static TextBlock Body(string text, double size = 14)
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

    public static TextBlock SectionLabel(string text) => Muted(text, 12);

    public static TextBox Field(string? placeholder = null, bool secret = false)
    {
        var box = new TextBox
        {
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 14,
            PlaceholderText = placeholder,
            PasswordChar = secret ? '•' : default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 40,
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

    public static Border Card(Control child, Thickness? padding = null)
    {
        var card = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = padding ?? new Thickness(14),
            CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
            BorderThickness = new Thickness(1),
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

    public static ComboBox Picker()
    {
        var picker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 40,
            FontFamily = GraphicalProfile.BodyFont,
        };
        return picker;
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
            MinHeight = 40,
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

    public static void ApplyStatusTone(TextBlock block, StatusTone tone)
    {
        var key = tone switch
        {
            StatusTone.Busy => GraphicalProfile.AccentResourceKey,
            StatusTone.Success => GraphicalProfile.ActionResourceKey,
            StatusTone.Warning => GraphicalProfile.WarningResourceKey,
            StatusTone.Danger => GraphicalProfile.DangerResourceKey,
            _ => GraphicalProfile.MutedResourceKey,
        };
        GraphicalProfileBinding.Bind(block, TextBlock.ForegroundProperty, key);
    }

    public static void StyleModeToggle(Button button, bool selected)
    {
        if (selected)
        {
            GraphicalProfileBinding.Bind(
                button,
                Avalonia.Controls.Button.BackgroundProperty,
                GraphicalProfile.AccentFillResourceKey);
            GraphicalProfileBinding.Bind(
                button,
                Avalonia.Controls.Button.ForegroundProperty,
                GraphicalProfile.OnAccentFillResourceKey);
            button.BorderThickness = new Thickness(0);
            button.FontWeight = FontWeight.SemiBold;
            return;
        }

        GraphicalProfileBinding.Bind(
            button,
            Avalonia.Controls.Button.BackgroundProperty,
            GraphicalProfile.RaisedResourceKey);
        GraphicalProfileBinding.Bind(
            button,
            Avalonia.Controls.Button.ForegroundProperty,
            GraphicalProfile.TextResourceKey);
        GraphicalProfileBinding.Bind(
            button,
            Avalonia.Controls.Button.BorderBrushProperty,
            GraphicalProfile.BorderResourceKey);
        button.BorderThickness = new Thickness(1);
        button.FontWeight = FontWeight.Normal;
    }
}
