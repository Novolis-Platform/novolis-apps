using System.Text.Json;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;

namespace PresenceLedger.App.Map;

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
