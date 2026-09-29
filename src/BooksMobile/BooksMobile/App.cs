using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BooksMobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BooksMobile;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = mainView,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
