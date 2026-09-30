using System.Text.Json;
using Avalonia.Media.Imaging;
using Novolis.Avalonia.Map;
using Novolis.Avalonia.Mobile;
using Novolis.Math.Geometry;

namespace PresenceLedger.App.Map;

/// <summary>Application-facing map search abstraction.</summary>
public interface IMapSearchProvider
{
    /// <summary>Searches for a human-entered place or address.</summary>
    Task<IReadOnlyList<MapSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
