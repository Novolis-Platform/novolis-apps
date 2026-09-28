using Avalonia.Controls;

namespace Novolis.Reach.Host.Windows;

/// <summary>Window hosting the Reach host operator dashboard.</summary>
public sealed class ReachHostWindow : Window
{
    /// <summary>Creates the operator window.</summary>
    public ReachHostWindow()
    {
        Title = "Novolis Reach Host";
        Width = 1040;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
