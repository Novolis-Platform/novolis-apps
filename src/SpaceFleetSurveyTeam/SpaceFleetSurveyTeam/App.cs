using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SpaceFleetSurveyTeam.Views;

namespace SpaceFleetSurveyTeam;

public sealed class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is null)
            throw new InvalidOperationException(
                "App.Services was not set before Avalonia initialization. Platform hosts must register DI before AppBuilder.Setup.");

        var fieldShell = Services.GetRequiredService<FieldShellView>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Content = fieldShell,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = fieldShell;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
