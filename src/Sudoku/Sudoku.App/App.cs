using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.GraphicalProfile;

namespace Sudoku.App;

/// <summary>Avalonia application shell for the Sudoku product.</summary>
public sealed class App : Application
{
    /// <summary>Services supplied by the executable host.</summary>
    public static IServiceProvider? Services { get; set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Services ?? throw new InvalidOperationException(
                "Sudoku services were not assigned before Avalonia startup.");
            desktop.MainWindow = services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
