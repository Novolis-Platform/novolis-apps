using Avalonia.Controls;
using CursorRemote.Ui;

namespace CursorRemote;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Cursor Remote";
        Width = 1080;
        Height = 760;
        MinWidth = 720;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = RemotePalette.WindowBrush;
        FontFamily = RemotePalette.BodyFont;
        Foreground = RemotePalette.BodyBrush;
    }
}
