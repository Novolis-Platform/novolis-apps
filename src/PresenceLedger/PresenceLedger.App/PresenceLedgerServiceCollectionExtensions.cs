using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;
using PresenceLedger.App.Map;
using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.App;

/// <summary>Registers the shared Presence Ledger application composition.</summary>
public static class PresenceLedgerServiceCollectionExtensions
{
    /// <summary>
    /// Registers local storage, inference, Kartverket adapters, and the shared view.
    /// A platform host must register <see cref="IAppDataPaths"/> first.
    /// </summary>
    public static IServiceCollection AddPresenceLedger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<HttpClient>(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PresenceLedger/2026.1");
            return client;
        });
        services.AddSingleton<NdjsonPresenceStorage>(sp =>
            new NdjsonPresenceStorage(
                sp.GetRequiredService<IAppDataPaths>().RootDirectory));
        services.AddSingleton<ITrackedLocationStore>(sp =>
            sp.GetRequiredService<NdjsonPresenceStorage>().Locations);
        services.AddSingleton<IPresenceEventStore>(sp =>
            sp.GetRequiredService<NdjsonPresenceStorage>().Events);
        services.AddSingleton<IPresenceStateStore>(sp =>
            sp.GetRequiredService<NdjsonPresenceStorage>().States);
        services.AddSingleton<IPresenceObservationStore>(sp =>
            sp.GetRequiredService<NdjsonPresenceStorage>().Observations);
        services.AddSingleton<IPresenceEngine>(sp =>
            new PresenceEngine(
                TimeProvider.System,
                sp.GetRequiredService<ITrackedLocationStore>(),
                sp.GetRequiredService<IPresenceStateStore>(),
                sp.GetRequiredService<IPresenceEventStore>()));
        services.AddSingleton<PresenceDayProjector>();
        services.AddSingleton<KartverketMapTileSource>();
        services.AddSingleton<IMapTileSource>(sp =>
            sp.GetRequiredService<KartverketMapTileSource>());
        services.AddSingleton<IMapSearchProvider, KartverketMapSearchProvider>();
        services.AddSingleton<PresenceObservationCoordinator>();
        services.AddSingleton<MainView>();
        return services;
    }
}
