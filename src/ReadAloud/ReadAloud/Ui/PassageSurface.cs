using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace ReadAloud.Ui;

/// <summary>
/// The passage. While a listen is running it is plain text, so it cannot take a caret.
/// </summary>
public sealed class PassageSurface : Grid
{
    readonly TextBox _editor;
    readonly TextBlock _reading;

    public PassageSurface()
    {
        _editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Paste or type anything to hear…",
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 18,
            MinHeight = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        GraphicalProfileBinding.Bind(_editor, TextBox.ForegroundProperty, GraphicalProfile.TextResourceKey);
        GraphicalProfileBinding.Bind(_editor, TextBox.BackgroundProperty, GraphicalProfile.RaisedResourceKey);
        GraphicalProfileBinding.Bind(_editor, TextBox.CaretBrushProperty, GraphicalProfile.AccentResourceKey);

        _reading = new TextBlock
        {
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
        };
        GraphicalProfileBinding.Bind(_reading, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);

        Children.Add(_editor);
        Children.Add(_reading);
    }

    public string Text
    {
        get => _editor.Text ?? string.Empty;
        set
        {
            _editor.Text = value;
            _reading.Text = value;
        }
    }

    public bool Locked
    {
        set
        {
            _reading.Text = _editor.Text;
            _editor.IsVisible = !value;
            _editor.IsEnabled = !value;
            _editor.IsHitTestVisible = !value;
            _editor.Focusable = !value;
            _reading.IsVisible = value;
            _reading.IsHitTestVisible = false;
        }
    }
}
