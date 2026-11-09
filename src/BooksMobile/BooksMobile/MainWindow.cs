using Avalonia.Controls;
using Novolis.Avalonia.GraphicalProfile;

namespace BooksMobile;

/// <summary>Desktop shell hosting <see cref="Views.MainView"/>.</summary>
public sealed class MainWindow : Window
{
    /// <summary>Galaxy Z Flip8 main panel (FHD+). Desktop uses half DIPs by default for fit.</summary>
    public const double Flip8NativeWidth = 1080;
    public const double Flip8NativeHeight = 2520;
    public const double Flip8Width = 540;
    public const double Flip8Height = 1260;

    public MainWindow()
    {
        var profile = DesktopDeviceProfile.Resolve(Environment.GetCommandLineArgs());
        Title = string.Equals(profile.Name, "desktop", StringComparison.Ordinal)
            ? "Novolis Books"
            : $"Novolis Books · {profile.Name}";
        ApplyDeviceProfile(profile);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = GraphicalProfile.BackgroundBrush;
        FontFamily = GraphicalProfile.BodyFont;
        Foreground = GraphicalProfile.TextBrush;
    }

    public void ApplyDeviceProfile(DesktopDeviceProfile profile)
    {
        Width = profile.Width;
        Height = profile.Height;
        MinWidth = Math.Min(320, profile.Width);
        MinHeight = Math.Min(568, profile.Height);
        CanResize = profile.CanResize;
    }
}
