using Novolis.Maui.GraphicalProfile;

namespace Novolis.Ndjson.App;

public sealed class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage, GraphicalProfileInstaller profileInstaller)
    {
        _mainPage = mainPage;
        profileInstaller.Install(this);
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_mainPage);
}
