using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>
/// A versioned, local-only point-in-time sample retained for diagnostics and
/// deterministic day replay.
/// </summary>
public sealed record PresenceObservationRecord
{
    /// <summary>Current serialized record version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Creates a retained observation.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public PresenceObservationRecord(
        DateTimeOffset at,
        GeoCoordinate? position,
        double? accuracyMeters,
        RecordedWifiStatus wifiStatus,
        string? connectedSsid,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (at == default)
            throw new ArgumentException("Observation time must be set.", nameof(at));

        if (accuracyMeters is { } accuracy
            && (!double.IsFinite(accuracy) || accuracy < 0))
            throw new ArgumentOutOfRangeException(nameof(accuracyMeters));

        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        At = at.ToUniversalTime();
        Position = position;
        AccuracyMeters = accuracyMeters;
        WifiStatus = wifiStatus;
        ConnectedSsid = string.IsNullOrWhiteSpace(connectedSsid)
            ? null
            : connectedSsid.Trim();
        SchemaVersion = schemaVersion;
    }

    /// <summary>UTC time at which the platform sample was accepted.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Position supplied by the platform, when available.</summary>
    public GeoCoordinate? Position { get; }

    /// <summary>Horizontal uncertainty supplied by the platform, when available.</summary>
    public double? AccuracyMeters { get; }

    /// <summary>Wi-Fi capability captured at the same point in time.</summary>
    public RecordedWifiStatus WifiStatus { get; }

    /// <summary>Connected SSID, retained only in the local private store.</summary>
    public string? ConnectedSsid { get; }

    /// <summary>Schema version used for the serialized record.</summary>
    public int SchemaVersion { get; }

    /// <summary>Creates a record from a position reading and current Wi-Fi state.</summary>
    public static PresenceObservationRecord FromPosition(
        PositionObservation position,
        RecordedWifiStatus wifiStatus = RecordedWifiStatus.Unknown,
        string? connectedSsid = null) =>
        new(
            position.At,
            position.Position,
            position.AccuracyMeters,
            wifiStatus,
            connectedSsid);

    /// <summary>Creates a record from a Wi-Fi reading without a position.</summary>
    public static PresenceObservationRecord FromWifi(
        WifiObservation wifi,
        RecordedWifiStatus status) =>
        new(
            wifi.At,
            null,
            null,
            status,
            wifi.ConnectedSsid);
}
