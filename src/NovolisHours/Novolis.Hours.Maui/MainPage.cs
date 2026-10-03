using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Hours.Maui;

/// <summary>Profile-compliant native client foundation for future authenticated Hours views.</summary>
public sealed class MainPage : ContentPage
{
    /// <summary>Initializes the client foundation page.</summary>
    public MainPage()
    {
        Title = "Novolis Hours";
        BackgroundColor = GraphicalProfile.Background;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(28),
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Novolis Hours",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = GraphicalProfile.Text,
                    },
                    new Label
                    {
                        Text = "Native mobile client foundation for the Novolis Hours service.",
                        FontSize = 16,
                        TextColor = GraphicalProfile.Muted,
                    },
                    new Border
                    {
                        Stroke = new SolidColorBrush(GraphicalProfile.Border),
                        Background = new SolidColorBrush(GraphicalProfile.Surface),
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                        Padding = 18,
                        Content = new Label
                        {
                            Text = "Connect to a secured Hours host to register presence, see flex saldo, and resolve approval anomalies.",
                            TextColor = GraphicalProfile.Text,
                        },
                    },
                },
            },
        };
    }
}
