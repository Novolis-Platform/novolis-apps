using Avalonia.Controls;
using ReadAloud.Ui;

namespace ReadAloud;

/// <summary>Desktop shell hosting <see cref="Views.MainView"/>.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Read Aloud";
        Width = 720;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ReadAloudPalette.WindowBrush;
        FontFamily = ReadAloudPalette.BodyFont;
        Foreground = ReadAloudPalette.BodyBrush;
    }
}
