using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace SinsOfACapitalismTycoon.Ui;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.UiOptions ?? Cli.RunOptions.Default;
            desktop.MainWindow = options.Engine == Cli.EngineKind.Campaign
                ? new MainWindow(options)
                : new CoreReportWindow(Program.ReportText);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
