using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile;

namespace NovolisMaps;

/// <summary>Registers the shared Novolis Maps composition.</summary>
public static class NovolisMapsServiceCollectionExtensions
{
    /// <summary>
    /// Registers local pins, map providers, and the shared Avalonia view.
    /// A platform host must register <see cref="IAppDataPaths" /> first.
    /// </summary>
    public static IServiceCollection AddNovolisMaps(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<HttpClient>(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "NovolisMaps/2026.1 (+https://github.com/Novolis-Platform/novolis-apps)");
            return client;
        });
        services.AddSingleton<MapSourceCatalog>();
        services.AddSingleton<MapPinStore>();
        services.AddSingleton<MainView>();
        return services;
    }
}
