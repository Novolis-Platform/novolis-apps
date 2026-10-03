using Microsoft.Maui;

namespace Novolis.Hours.Maui.WinUI;

/// <summary>WinUI entry point for the MAUI client.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes the WinUI application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
