using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.GraphicalProfile;

namespace LiveStudio;

internal sealed class App : Application
{
    public const string AgentEndpoint = "novolis-avalonia-agent-live";

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
            desktop.MainWindow = new MainWindow(Program.Runtime.Session);
            Novolis.Apps.Branding.AppBrand.ApplyWindowIcon(desktop.MainWindow);
            if (AgentHost.IsEnabledByEnvironment())
                s_agentHost = AgentHost.Attach(desktop.MainWindow, AgentEndpoint);
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e) =>
        Program.Runtime.RequestShutdown();
}
