using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Hours.Client.Maui;

/// <summary>Composes the cross-platform Novolis Hours client foundation.</summary>
public static class MauiProgram
{
    /// <summary>Builds the MAUI application using the platform graphical profile.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseGraphicalProfile();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
