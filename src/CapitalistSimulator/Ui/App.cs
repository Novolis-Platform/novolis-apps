using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using CapitalistSimulator.Cli;
using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Ui;

internal sealed class App : Application
{
    private static RunOptions _options = new();
    private static GameWorld? _world;

    public static void Configure(RunOptions options, GameWorld world)
    {
        _options = options;
        _world = world;
    }

    public override void Initialize()
    {
        // Code-only; no AXAML required
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var world = _world ?? WorldFactory.Create(
                _options.Scenario,
                _options.StartingCash,
                _options.AiCount,
                _options.AiAggressiveness,
                _options.Seed);
            desktop.MainWindow = new MainWindow(world, _options);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
