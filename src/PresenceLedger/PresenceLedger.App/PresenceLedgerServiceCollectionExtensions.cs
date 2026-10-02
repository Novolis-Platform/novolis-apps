using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;
using PresenceLedger.Core;
using PresenceLedger.Storage;

namespace PresenceLedger.App;

/// <summary>Registers the shared Presence Ledger application composition.</summary>
public static class PresenceLedgerServiceCollectionExtensions
{
    /// <summary>
    /// Registers local storage, inference, map providers, and the shared view.
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
        services.AddSingleton<IMapRasterSource>(sp =>
            new XyzMapSource(
                sp.GetRequiredService<HttpClient>(),
                MapPresets.KartverketTopo,
                Path.Combine(
                    sp.GetRequiredService<IAppDataPaths>().RootDirectory,
                    "cache",
                    "maps"),
                "PresenceLedger/2026.1"));
        services.AddSingleton<RasterMapTileSource>();
        services.AddSingleton<IMapTileSource>(sp =>
            sp.GetRequiredService<RasterMapTileSource>());
        services.AddSingleton<IMapPlaceSearch, GeonorgeAddressSearch>();
        services.AddSingleton<PresenceHistoryRebuild>();
        services.AddSingleton<PresenceObservationCoordinator>();
        services.AddSingleton<ILedgerFilePublisher>(sp =>
            DirectoryLedgerFilePublisher.ForDownloads(
                sp.GetRequiredService<IAppDataPaths>().RootDirectory));
        services.AddSingleton<MainView>();
        return services;
    }
}
