using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using SinsOfACapitalismTycoon.Cli;

namespace SinsOfACapitalismTycoon.Ui;

public sealed class App : Application
{
    static AgentHost? s_agentHost;

    /// <summary>Set once before desktop lifetime starts.</summary>
    internal static RunOptions RunOptions { get; private set; } = RunOptions.Default;

    public static string? CoreReportText { get; private set; }

    internal static void Configure(RunOptions options, string? coreReportText)
    {
        RunOptions = options ?? RunOptions.Default;
        CoreReportText = coreReportText;
    }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = RunOptions;
            Window window = options.Engine == EngineKind.Campaign
                ? new MainWindow(options)
                : new CoreReportWindow(CoreReportText ?? "");
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
