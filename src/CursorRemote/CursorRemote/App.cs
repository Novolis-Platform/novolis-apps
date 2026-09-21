using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CursorRemote.Services;
using CursorRemote.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace CursorRemote;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
        {
            throw new InvalidOperationException(
                "App.Services must be assigned before Avalonia starts.");
        }

        var remoteView = Services.GetRequiredService<RemoteView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Content = remoteView,
            };
            Novolis.Apps.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
            Services.GetService<IHostDesktopChrome>()?.AttachMainWindow(desktop.MainWindow);
            Services.GetService<HostActivityLog>()?.Info("Host window ready.");
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = remoteView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
