using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.Reach.Host.Windows;

/// <summary>Avalonia operator application for the Reach host service.</summary>
public sealed class App : Application
{
    /// <summary>Host services supplied by the process entry point.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <summary>Whether the management companion should start minimized to its tray icon.</summary>
    public static bool StartInTray { get; set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GraphicalProfile.Install(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var view = Services.GetRequiredService<ReachHostView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new ReachHostWindow { Content = view };
            var tray = Services.GetRequiredService<ReachHostTrayController>();
            tray.Attach(window);
            desktop.MainWindow = window;
            if (StartInTray)
                Dispatcher.UIThread.Post(tray.HideToTray);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
