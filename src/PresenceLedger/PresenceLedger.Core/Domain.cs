using System.Text.Json.Serialization;
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
        PresencePolicy policy,
        DateTimeOffset? effectiveFromUtc = null,
        DateTimeOffset? effectiveToUtc = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Location id must not be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (effectiveFromUtc is { } from
            && effectiveToUtc is { } to
            && to <= from)
            throw new ArgumentException(
                "The location validity end must be after its start.",
                nameof(effectiveToUtc));

        DisplayName = displayName.Trim();
        Id = id;
        Area = area;
        Wifi = wifi;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        EffectiveFromUtc = effectiveFromUtc?.ToUniversalTime();
        EffectiveToUtc = effectiveToUtc?.ToUniversalTime();
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

    /// <summary>UTC instant at which this location revision becomes effective.</summary>
    public DateTimeOffset? EffectiveFromUtc { get; }

    /// <summary>UTC instant at which this location revision stops being effective.</summary>
    public DateTimeOffset? EffectiveToUtc { get; }
}
