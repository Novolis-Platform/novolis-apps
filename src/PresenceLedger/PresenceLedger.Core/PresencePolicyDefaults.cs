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
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(10));

    /// <summary>
    /// Upgrades locations written by the original GPS-only policy. Android can
    /// provide useful fixes several minutes apart while the phone is moving
    /// between foreground and background, so the old two-minute gap could never
    /// confirm a sustained visit.
    /// </summary>
    public static TrackedLocation UpgradeLegacyLocationOnly(TrackedLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Wifi is not null
            || location.Policy.ConfirmationDuration != TimeSpan.FromMinutes(5)
            || location.Policy.DepartureDuration != TimeSpan.FromMinutes(3)
            || location.Policy.MaximumEvidenceGap != TimeSpan.FromMinutes(2))
        {
            return location;
        }

        return new TrackedLocation(
            location.Id,
            location.DisplayName,
            location.Area,
            location.Wifi,
            LocationOnly);
    }
}
