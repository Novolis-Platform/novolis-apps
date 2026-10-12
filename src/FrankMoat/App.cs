using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using FrankMoat.Input;
using Novolis.Avalonia.GraphicalProfile;

namespace FrankMoat;

public sealed class App : Application
{
    public static Func<IGamepadReader?>? GamepadReaderFactory { get; set; }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(GamepadReaderFactory?.Invoke());
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new FrankMoatView(GamepadReaderFactory?.Invoke());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
