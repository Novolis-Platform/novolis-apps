using Avalonia.Controls;
using Ngp = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace Novolis.Avalonia.Reach;

/// <summary>Desktop window hosting the shared Reach client surface.</summary>
public sealed class ReachClientWindow : Window
{
    /// <summary>Creates the client window.</summary>
    public ReachClientWindow()
    {
        Title = ReachBranding.ProductName;
        Width = 1280;
        Height = 840;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ngp.ApplyWindowChrome(this);
    }
}
