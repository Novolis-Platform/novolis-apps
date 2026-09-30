using System.Text.Json;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;

namespace PresenceLedger.App.Map;

/// <summary>A result from an address search provider.</summary>
public sealed record MapSearchResult(string DisplayName, GeoCoordinate Coordinate);
