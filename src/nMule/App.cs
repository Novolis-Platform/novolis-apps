using Novolis.Maui.GraphicalProfile;

namespace Novolis.NMule;

/// <summary>nMule application root.</summary>
public sealed class App : Application
{
    private readonly MainPage _mainPage;

    /// <summary>Creates the application and installs the shared profile.</summary>
    public App(
        MainPage mainPage,
        GraphicalProfileInstaller profileInstaller)
    {
        _mainPage = mainPage;
        profileInstaller.Install(this);
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_mainPage);
}
