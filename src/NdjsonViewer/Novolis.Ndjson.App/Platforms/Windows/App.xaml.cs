using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Novolis.Ndjson.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        var executable = Environment.ProcessPath;
        var installed = !string.IsNullOrWhiteSpace(executable)
            && executable.StartsWith(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Novolis"),
                StringComparison.OrdinalIgnoreCase);
        if (installed
            || string.Equals(
                Environment.GetEnvironmentVariable("NOVOLIS_NDJSON_REGISTER_FILE_ASSOCIATIONS"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            WindowsFileAssociations.Register();
        }
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
