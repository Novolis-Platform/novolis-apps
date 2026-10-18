using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Novolis.Maui.GraphicalProfile;
using Novolis.NMule.Core;

namespace Novolis.NMule;

/// <summary>Composes the nMule Android host.</summary>
public static class MauiProgram
{
    /// <summary>Builds the nMule MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseGraphicalProfile();

#if ANDROID
        builder.Services.AddSingleton<AndroidNmuleNetworkGate>();
        builder.Services.AddSingleton<INmuleNetworkGate>(
            services => services.GetRequiredService<AndroidNmuleNetworkGate>());
#endif

        builder.Services
            .AddSingleton<NmuleSession>(services =>
                new NmuleSession(
                    services.GetRequiredService<INmuleNetworkGate>(),
                    NmuleStoragePaths.FromRoot(
                        Path.Combine(
                            FileSystem.AppDataDirectory,
                            "profile"))))
            .AddSingleton<MainPage>();
        return builder.Build();
    }
}
