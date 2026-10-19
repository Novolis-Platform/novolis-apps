using Avalonia.Controls;

namespace NovolisMaps;

/// <summary>Windows host window for Novolis Maps.</summary>
internal sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Novolis Maps";
        MinWidth = 720;
        MinHeight = 560;
    }
}
