using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Novolis.Avalonia.GraphicalProfile;
using Microsoft.Extensions.DependencyInjection;
using ReadAloud.Views;

namespace ReadAloud;

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
            single.MainView = new ScrollViewer
            {
                // Keep the mobile surface constrained to the display width so
                // WrapPanel rows can wrap instead of creating a wider canvas
                // whose left edge is clipped on Android.
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = mainView,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
