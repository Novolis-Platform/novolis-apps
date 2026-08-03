using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using GeoPolity.Session;
using Novolis.Avalonia.Agent;

namespace GeoPolity.AvaloniaUi;

public sealed class GeoPolityApp : Application
{
    internal static GeoSession? Session { get; set; }
    internal static AgentHost? UiAgent { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = Session ?? throw new InvalidOperationException("GeoPolity session not configured.");
            var window = new MainWindow(session);
            desktop.MainWindow = window;
            UiAgent = AgentHost.Attach(window, "novolis-avalonia-agent-geopolity");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
