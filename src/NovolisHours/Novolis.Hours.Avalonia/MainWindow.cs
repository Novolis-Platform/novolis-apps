using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.Hours.Avalonia;

/// <summary>Small native entry surface that guides the user to a self-hosted Hours service.</summary>
public sealed class MainWindow : Window
{
    /// <summary>Initializes the native client shell.</summary>
    public MainWindow()
    {
        Title = "Novolis Hours";
        Width = 720;
        Height = 420;
        MinWidth = 520;
        MinHeight = 320;
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var endpoint = new TextBox
        {
            Text = "http://localhost:5000",
            PlaceholderText = "Hours service URL",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Novolis Hours",
                    FontSize = 28,
                    FontWeight = FontWeight.SemiBold,
                },
                new TextBlock
                {
                    Text = "Native client foundation. Connect it to the self-contained Hours service to view records, notices, and approvals.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock { Text = "Service URL" },
                endpoint,
                new TextBlock
                {
                    Text = "The first delivery is the browser SPA; this shell is the shared-profile Avalonia foundation for a native client.",
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        Content = content;
    }
}
