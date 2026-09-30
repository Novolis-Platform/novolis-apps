using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Standard policies used by the first application flow.</summary>
public static class PresencePolicyDefaults
{
    /// <summary>Three-minute corroborated confirmation and departure.</summary>
    public static readonly PresencePolicy Standard = new(
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(3));

    /// <summary>Stronger location-only confirmation policy.</summary>
    public static readonly PresencePolicy LocationOnly = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(3));
}
