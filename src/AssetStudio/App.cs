using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.GraphicalProfile;

namespace AssetStudio;

internal sealed class App : Application
{
    public const string AgentEndpoint = "novolis-avalonia-agent-asset";

    static AgentHost? s_agentHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            if (AgentHost.IsEnabledByEnvironment())
                s_agentHost = AgentHost.Attach(desktop.MainWindow, AgentEndpoint);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
