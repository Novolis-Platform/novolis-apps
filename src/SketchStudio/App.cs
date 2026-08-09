using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Agent;

namespace SketchStudio;

public class App : Application
{
    public const string AgentEndpoint = "novolis-avalonia-agent-sketch";

    static AgentHost? s_agentHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            CrashGuard.InstallAvalonia(Dispatcher.UIThread);
            if (AgentHost.IsEnabledByEnvironment())
                s_agentHost = AgentHost.Attach(window, AgentEndpoint);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
