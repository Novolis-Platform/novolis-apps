using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SinsOfACapitalismTycoon.Ui;

internal sealed class MainWindow : Window
{
    public MainWindow(string reportText)
    {
        Title = "Sins of a Capitalism Tycoon";
        Width = 900;
        Height = 700;
        MinWidth = 480;
        MinHeight = 320;

        var header = new TextBlock
        {
            Text = "Sins of a Capitalism Tycoon",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(16, 16, 16, 8)
        };

        var subtitle = new TextBlock
        {
            Text = "Headless simulation report (same text as --mode headless)",
            Opacity = 0.7,
            Margin = new Thickness(16, 0, 16, 12)
        };

        var report = new TextBox
        {
            Text = reportText,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
            FontSize = 13,
            Margin = new Thickness(16, 0, 16, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                header,
                subtitle,
                report
            }
        };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(subtitle, Dock.Top);
    }
}
