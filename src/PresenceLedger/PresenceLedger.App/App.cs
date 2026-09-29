using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace PresenceLedger.App;

/// <summary>Avalonia application root shared by the local and Android heads.</summary>
public sealed class App : Application
{
    /// <summary>Services initialized by the platform host before Avalonia starts.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
            throw new InvalidOperationException(
                "App.Services must be initialized before Avalonia starts.");

        var mainView = Services.GetRequiredService<MainView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { Content = mainView };
            Novolis.Apps.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
