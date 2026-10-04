using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Novolis.Avalonia.GraphicalProfile;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Agent;

namespace CadStudio;

public sealed class App : Application
{
    static AgentHost? s_agentHost;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.ApplicationHost.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            Novolis.Apps.Branding.AppBrand.ApplyWindowIcon(window);
            s_agentHost = AgentHost.TryAttachFromEnvironment(window);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
