using System.Text.Json;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;

namespace PresenceLedger.App.Map;

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
