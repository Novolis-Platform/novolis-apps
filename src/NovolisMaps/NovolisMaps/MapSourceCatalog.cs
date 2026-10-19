using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.IO.Maps;

namespace NovolisMaps;

/// <summary>Creates the provider-neutral map stack used by Novolis Maps.</summary>
internal sealed class MapSourceCatalog : IDisposable
{
    readonly IReadOnlyList<MapProviderDefinition> _providers;

    public MapSourceCatalog(HttpClient httpClient, IAppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(paths);

        _providers =
        [
            Create(
                httpClient,
                paths,
                "topographic",
                "Topographic",
                "Kartverket topographic imagery · Norwegian address search",
                MapPresets.KartverketTopo,
                static client => new GeonorgeAddressSearch(client)),
            Create(
                httpClient,
                paths,
                "street",
                "Street",
                "OpenStreetMap street imagery · worldwide place search",
                MapPresets.OpenStreetMapStandard,
                static client => new NominatimPlaceSearch(client)),
        ];
    }

    /// <summary>Providers in the order shown by the map-style picker.</summary>
    public IReadOnlyList<MapProviderDefinition> Providers => _providers;

    /// <summary>The opening provider, selected for a useful Norwegian first view.</summary>
    public MapProviderDefinition Default => _providers[0];

    static MapProviderDefinition Create(
        HttpClient httpClient,
        IAppDataPaths paths,
        string id,
        string displayName,
        string description,
        XyzMapTemplate template,
        Func<HttpClient, IMapPlaceSearch> searchFactory)
    {
        var raster = new XyzMapSource(
            httpClient,
            template,
            Path.Combine(paths.CacheDirectory, "maps", id),
            "NovolisMaps/2026.1");
        return new MapProviderDefinition(
            id,
            displayName,
            description,
            raster,
            new RasterMapTileSource(raster),
            searchFactory(httpClient));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            if (provider.RasterSource is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
