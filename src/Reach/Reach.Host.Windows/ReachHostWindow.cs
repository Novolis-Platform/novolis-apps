using Avalonia.Controls;
using Ngp = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

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
        Ngp.ApplyWindowChrome(this);
    }
}
