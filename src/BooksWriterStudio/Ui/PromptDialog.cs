using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace BooksWriterStudio.Ui;

/// <summary>Minimal single-line text prompt for structure surgery titles.</summary>
internal static class PromptDialog
{
    public static async Task<string?> AskAsync(Window owner, string title, string prompt, string? initial = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var box = new TextBox
        {
            Text = initial ?? string.Empty,
            PlaceholderText = prompt,
            MinWidth = 320,
            Margin = new Avalonia.Thickness(0, 8, 0, 12),
        };

        string? result = null;
        var window = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) =>
        {
            result = box.Text?.Trim();
            window.Close();
        };
        cancel.Click += (_, _) =>
        {
            result = null;
            window.Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { ok, cancel },
        };

        window.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap },
                box,
                buttons,
            },
        };

        await window.ShowDialog(owner);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
