using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace PresenceLedger.App;

/// <summary>Shared Presence Ledger chrome primitives.</summary>
internal static class LedgerChrome
{
    public static StackPanel Page(string eyebrow, string title, string subtitle)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(16, 10, 16, 16),
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        stack.Children.Add(Eyebrow(eyebrow));
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Classes = { "page-title" },
            Foreground = GraphicalProfile.TextBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Classes = { "body-copy" },
            Foreground = GraphicalProfile.MutedBrush,
        });
        return stack;
    }

    public static TextBlock Eyebrow(string text) =>
        new()
        {
            Text = text,
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
            Margin = new Thickness(0, 4, 0, 0),
        };

    public static TextBlock Muted(string text) =>
        new()
        {
            Text = text,
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        };

    public static TextBlock Body(string text) =>
        new()
        {
            Text = text,
            Classes = { "body-copy" },
            Foreground = GraphicalProfile.TextBrush,
        };

    public static Border Card(Control child, IBrush? background = null) =>
        new()
        {
            Child = child,
            Padding = new Thickness(16),
            Background = background ?? GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
        };

    public static Border Chip(string text, bool accent = false) =>
        new()
        {
            Padding = new Thickness(10, 5),
            Background = accent
                ? GraphicalProfile.AccentFillBrush
                : GraphicalProfile.ActionSoftBrush,
            CornerRadius = new CornerRadius(14),
            Child = new TextBlock
            {
                Text = text,
                Classes = { "eyebrow" },
                Foreground = GraphicalProfile.TextBrush,
            },
        };

    public static Button Primary(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = GraphicalProfileColors.TouchTarget,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.OnAccentFillBrush,
        };
        button.Click += (_, _) => action();
        return button;
    }

    public static Button Secondary(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Classes = { "secondary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = GraphicalProfileColors.TouchTarget,
            Background = GraphicalProfile.RaisedBrush,
            Foreground = GraphicalProfile.TextBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
        };
        button.Click += (_, _) => action();
        return button;
    }

    public static Control Metric(string label, string value)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.MutedBrush,
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
        });
        return stack;
    }

    public static Control MetricsRow(params (string Label, string Value)[] metrics)
    {
        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 12,
            LineSpacing = 8,
        };
        foreach (var metric in metrics)
            wrap.Children.Add(Metric(metric.Label, metric.Value));

        return Card(wrap);
    }
}
