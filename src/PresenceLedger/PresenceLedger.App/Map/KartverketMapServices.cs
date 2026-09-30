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
