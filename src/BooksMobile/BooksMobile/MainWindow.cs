using Avalonia.Controls;
using BooksMobile.Ui;

namespace BooksMobile;

/// <summary>Desktop shell hosting <see cref="Views.MainView"/>.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Novolis Books";
        Width = 1100;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BooksPalette.WindowBrush;
        FontFamily = BooksPalette.BodyFont;
        Foreground = BooksPalette.BodyBrush;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.png");
        if (File.Exists(iconPath))
        {
            using var stream = File.OpenRead(iconPath);
            Icon = new WindowIcon(stream);
        }
    }
}
