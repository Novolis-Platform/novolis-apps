using Novolis.Avalonia.Map;
using Novolis.IO.Maps;

namespace NovolisMaps;

/// <summary>A map imagery and search pairing available to the product.</summary>
internal sealed record MapProviderDefinition(
    string Id,
    string DisplayName,
    string Description,
    IMapRasterSource RasterSource,
    IMapTileSource TileSource,
    IMapPlaceSearch Search)
{
    public override string ToString() => DisplayName;
}
