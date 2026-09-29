using System.Text.Json;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;

namespace PresenceLedger.App.Map;

/// <summary>Kartverket map attribution and endpoint constants.</summary>
public static class KartverketMap
{
    /// <summary>Attribution shown whenever Kartverket map content is visible.</summary>
    public const string Attribution = "© Kartverket";

    /// <summary>HTTPS WMTS template for the topographic map.</summary>
    public const string TileTemplate =
        "https://cache.kartverket.no/v1/wmts/1.0.0/topo/default/webmercator/{z}/{y}/{x}.png";

    /// <summary>Official address search endpoint.</summary>
    public const string SearchEndpoint = "https://ws.geonorge.no/adresser/v1/sok";
}

/// <summary>Application-owned Kartverket raster tile source with a private cache.</summary>
public sealed class KartverketMapTileSource : IMapTileSource
{
    readonly HttpClient _httpClient;
    readonly string _cacheDirectory;

    /// <summary>Creates a source under the application's private cache.</summary>
    public KartverketMapTileSource(
        HttpClient httpClient,
        IAppDataPaths appDataPaths)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(appDataPaths);
        _cacheDirectory = Path.Combine(appDataPaths.RootDirectory, "cache", "maps");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <inheritdoc />
    public async ValueTask<MapTile?> GetTileAsync(
        MapTileKey key,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(
            _cacheDirectory,
            key.Zoom.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            key.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            $"{key.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}.png");
        byte[] bytes;

        if (File.Exists(path))
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        else
        {
            var uri = BuildTileUri(key);
            try
            {
                bytes = await _httpClient.GetByteArrayAsync(uri, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }

        await using var stream = new MemoryStream(bytes, writable: false);
        return new MapTile(key, new Bitmap(stream));
    }

    /// <summary>Builds the official tile URI for a normalized tile key.</summary>
    public static Uri BuildTileUri(MapTileKey key) =>
        new(
            KartverketMap.TileTemplate
                .Replace("{z}", key.Zoom.ToString(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{x}", key.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{y}", key.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
}

/// <summary>A result from an address search provider.</summary>
public sealed record MapSearchResult(string DisplayName, GeoCoordinate Coordinate);

/// <summary>Application-facing map search abstraction.</summary>
public interface IMapSearchProvider
{
    /// <summary>Searches for a human-entered place or address.</summary>
    Task<IReadOnlyList<MapSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}

/// <summary>Kartverket/Geonorge address search adapter.</summary>
public sealed class KartverketMapSearchProvider : IMapSearchProvider
{
    readonly HttpClient _httpClient;

    /// <summary>Creates a search provider.</summary>
    public KartverketMapSearchProvider(HttpClient httpClient) =>
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MapSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var uri =
            $"{KartverketMap.SearchEndpoint}?sok={Uri.EscapeDataString(query.Trim())}"
            + "&treffPerSide=10&side=0";

        string json;
        try
        {
            json = await _httpClient.GetStringAsync(uri, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("adresser", out var addresses)
            || addresses.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<MapSearchResult>();
        foreach (var address in addresses.EnumerateArray())
        {
            if (!address.TryGetProperty("adressetekst", out var label)
                || !address.TryGetProperty("representasjonspunkt", out var point)
                || !point.TryGetProperty("lat", out var latitude)
                || !point.TryGetProperty("lon", out var longitude)
                || !latitude.TryGetDouble(out var lat)
                || !longitude.TryGetDouble(out var lon))
                continue;

            try
            {
                results.Add(new MapSearchResult(
                    label.GetString() ?? "Unnamed address",
                    new GeoCoordinate(lat, lon)));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ignore malformed provider entries.
            }
        }

        return results;
    }
}
