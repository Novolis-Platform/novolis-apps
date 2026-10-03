using Novolis.Maui.GraphicalProfile;

namespace Novolis.Hours.Maui;

/// <summary>Hosts the first MAUI page for the Novolis Hours client.</summary>
public sealed class App : Application
{
    private readonly MainPage mainPage;

    /// <summary>Initializes the app with the dependency-injected main page.</summary>
    public App(MainPage mainPage, GraphicalProfileInstaller profileInstaller)
    {
        this.mainPage = mainPage;
        profileInstaller.Install(this);
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(mainPage) { Title = "Novolis Hours" };
}
