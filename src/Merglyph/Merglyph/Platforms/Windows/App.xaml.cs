using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Merglyph.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        var webViewData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "merglyph",
            "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);

        // Opt-in only: MERGLYPH_REGISTER_FILE_ASSOCIATIONS=1 (or "true") for local debug.
        var register = Environment.GetEnvironmentVariable("MERGLYPH_REGISTER_FILE_ASSOCIATIONS");
        if (string.Equals(register, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(register, "true", StringComparison.OrdinalIgnoreCase))
        {
            WindowsFileAssociations.Register();
        }

        var unregister = Environment.GetEnvironmentVariable("MERGLYPH_UNREGISTER_FILE_ASSOCIATIONS");
        if (string.Equals(unregister, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unregister, "true", StringComparison.OrdinalIgnoreCase))
        {
            WindowsFileAssociations.Unregister();
        }

        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        base.OnLaunched(args);
        WindowsFileActivation.Publish(activation);
        WindowsFileActivation.Publish(Environment.GetCommandLineArgs().Skip(1));
    }
}
