using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>A user-defined location whose evidence can be evaluated independently.</summary>
public sealed record TrackedLocation
{
    /// <summary>Creates a tracked location.</summary>
    public TrackedLocation(
        Guid id,
        string displayName,
        GeoCircle area,
        WifiEvidence? wifi,
        PresencePolicy policy)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Location id must not be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName.Trim();
        Id = id;
        Area = area;
        Wifi = wifi;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>Stable location identifier.</summary>
    public Guid Id { get; }

    /// <summary>User-facing name with no special semantic meaning.</summary>
    public string DisplayName { get; }

    /// <summary>Configured geographic area.</summary>
    public GeoCircle Area { get; }

    /// <summary>Optional connected-network corroboration.</summary>
    public WifiEvidence? Wifi { get; }

    /// <summary>Inference policy for this location.</summary>
    public PresencePolicy Policy { get; }
}
