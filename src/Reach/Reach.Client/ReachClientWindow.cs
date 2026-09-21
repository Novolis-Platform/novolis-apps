using Avalonia.Controls;

namespace Novolis.Reach.Client;

/// <summary>Desktop window hosting the shared Reach client surface.</summary>
public sealed class ReachClientWindow : Window
{
    /// <summary>Creates the client window.</summary>
    public ReachClientWindow()
    {
        Title = "Novolis Reach";
        Width = 1280;
        Height = 840;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
