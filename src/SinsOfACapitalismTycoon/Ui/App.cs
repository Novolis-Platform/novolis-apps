using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;

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
            // Dedicated pipe so Draft Studio (default novolis-avalonia-agent) cannot steal the desk.
            if (AgentHost.IsEnabledByEnvironment())
            {
                var endpoint = Environment.GetEnvironmentVariable(UiTransportEndpoints.EndpointEnvVar);
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = "novolis-avalonia-agent-sins";
                }

                s_agentHost = AgentHost.Attach(window, endpoint);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
