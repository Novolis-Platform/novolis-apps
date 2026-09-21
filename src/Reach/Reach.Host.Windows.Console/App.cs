using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Reach.Host.Windows.Console;

/// <summary>Avalonia operator application for the Reach host service.</summary>
public sealed class App : Application
{
    /// <summary>Host services supplied by the process entry point.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var view = Services.GetRequiredService<ReachHostConsoleView>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ReachHostConsoleWindow { Content = view };
        base.OnFrameworkInitializationCompleted();
    }
}
