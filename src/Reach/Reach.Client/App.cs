using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Reach.Client;

/// <summary>Shared Avalonia application for Reach clients.</summary>
public sealed class App : Application
{
    /// <summary>Services supplied by the platform host.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var view = Services.GetRequiredService<ReachClientView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ReachClientWindow { Content = view };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = view;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
