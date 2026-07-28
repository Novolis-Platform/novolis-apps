using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LiveStudio.Components.Live;

/// <summary>Child window hosting an <see cref="ILiveVisualizer"/>.</summary>
public sealed class LiveVisualizerWindow : Window
{
    readonly ILiveVisualizer _visualizer;

    public LiveVisualizerWindow(ILiveVisualizer visualizer)
    {
        _visualizer = visualizer;
        Title = $"Live — {visualizer.Title}";
        Width = 720;
        Height = 480;
        MinWidth = 420;
        MinHeight = 280;
        Background = new SolidColorBrush(Color.Parse("#0B1220"));

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = visualizer.Title,
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White,
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#111827")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#334155")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(12),
                        Child = visualizer.View,
                        VerticalAlignment = VerticalAlignment.Stretch,
                    },
                },
            },
        };
    }

    public void Bind(LiveVisualizerModel model) => _visualizer.Bind(model);
}
