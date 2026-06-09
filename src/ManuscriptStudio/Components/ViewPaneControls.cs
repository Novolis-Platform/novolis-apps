using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Markdown;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Components;

/// <summary>Shared zoom and wrap controls for editor and preview action bars.</summary>
internal static class ViewPaneControls
{
    public static void AddWordWrapToggle(StackPanel bar, ToggleButton toggle, EventHandler<RoutedEventArgs> onClick)
    {
        toggle.Content = "Wrap";
        toggle.Padding = new Thickness(8, 4);
        toggle.Margin = new Thickness(0, 0, 4, 0);
        toggle.Click += onClick;
        bar.Children.Add(toggle);
    }

    public static void AddZoomCluster(
        StackPanel bar,
        string prefix,
        Button zoomOut,
        Button zoomIn,
        Button zoomReset,
        TextBlock label,
        EventHandler<RoutedEventArgs> onZoomOut,
        EventHandler<RoutedEventArgs> onZoomIn,
        EventHandler<RoutedEventArgs> onZoomReset)
    {
        zoomOut.Content = "−";
        zoomOut.Padding = new Thickness(8, 4);
        zoomOut.Margin = new Thickness(0, 0, 2, 0);
        zoomOut.Click += onZoomOut;

        zoomIn.Content = "+";
        zoomIn.Padding = new Thickness(8, 4);
        zoomIn.Margin = new Thickness(0, 0, 2, 0);
        zoomIn.Click += onZoomIn;

        zoomReset.Content = "100%";
        zoomReset.Padding = new Thickness(8, 4);
        zoomReset.Margin = new Thickness(0, 0, 4, 0);
        zoomReset.Click += onZoomReset;

        label.Text = "100%";
        label.Margin = new Thickness(2, 0, 4, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Opacity = 0.85;

        bar.Children.Add(new TextBlock
        {
            Text = prefix,
            Margin = new Thickness(4, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7,
        });
        bar.Children.Add(zoomOut);
        bar.Children.Add(zoomIn);
        bar.Children.Add(zoomReset);
        bar.Children.Add(label);
    }

    public static void UpdateZoomLabel(TextBlock label, double scale) =>
        label.Text = $"{Math.Round(Math.Clamp(scale, MarkdownZoom.Minimum, MarkdownZoom.Maximum) * 100)}%";

    public static Button CreateActionButton(string label) =>
        new()
        {
            Content = label,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(10, 4),
        };
}
