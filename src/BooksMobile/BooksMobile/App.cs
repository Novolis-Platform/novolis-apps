using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.GraphicalProfile;

namespace BooksMobile;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
            throw new InvalidOperationException(
                "App.Services was not set before Avalonia initialization. Platform hosts must register DI before AppBuilder.Setup.");

        var mainView = Services.GetRequiredService<MainView>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Content = mainView,
            };
            Novolis.Apps.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            // MainView and its individual screens own their scrolling. Wrapping the
            // entire authoring shell in a second ScrollViewer measures the star row
            // with an infinite height on Android, which breaks the narrow layout and
            // can leave only the status row visible after an async error.
            single.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
