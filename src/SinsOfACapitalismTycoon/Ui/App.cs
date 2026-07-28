using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Agent;

namespace SinsOfACapitalismTycoon.Ui;

public sealed class App : Application
{
    static AgentHost? s_agentHost;

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
            Window window = options.Engine == Cli.EngineKind.Campaign
                ? new MainWindow(options)
                : new CoreReportWindow(Program.ReportText);
            desktop.MainWindow = window;
            s_agentHost = AgentHost.TryAttachFromEnvironment(window);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
