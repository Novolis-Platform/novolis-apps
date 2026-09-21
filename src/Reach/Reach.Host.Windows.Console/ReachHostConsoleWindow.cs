using Avalonia.Controls;

namespace Novolis.Reach.Host.Windows.Console;

/// <summary>Window hosting the Reach host operator dashboard.</summary>
public sealed class ReachHostConsoleWindow : Window
{
    /// <summary>Creates the operator window.</summary>
    public ReachHostConsoleWindow()
    {
        Title = "Novolis Reach Host";
        Width = 1040;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
