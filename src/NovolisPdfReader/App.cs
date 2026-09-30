using Novolis.Maui.Agent;
using Novolis.Maui.GraphicalProfile;

namespace NovolisPdfReader;

/// <summary>Novolis PDF Reader application root.</summary>
public sealed class App : Application
{
    private readonly MainPage _mainPage;
    private AgentHost? _agentHost;

    /// <summary>Creates the branded application root.</summary>
    public App(MainPage mainPage, GraphicalProfileInstaller profileInstaller)
    {
        _mainPage = mainPage;
        profileInstaller.Install(this);
        CrashGuard.Install("NovolisPdfReader");
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_mainPage)
        {
            Title = "Novolis PDF Reader",
        };
        _agentHost = AgentHost.Attach(_mainPage);
        return window;
    }
}
