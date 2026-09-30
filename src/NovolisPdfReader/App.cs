using Novolis.Maui.GraphicalProfile;

namespace NovolisPdfReader;

/// <summary>Novolis PDF Reader application root.</summary>
public sealed class App : Application
{
    private readonly MainPage _mainPage;

    /// <summary>Creates the branded application root.</summary>
    public App(MainPage mainPage, GraphicalProfileInstaller profileInstaller)
    {
        _mainPage = mainPage;
        profileInstaller.Install(this);
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_mainPage);
}
