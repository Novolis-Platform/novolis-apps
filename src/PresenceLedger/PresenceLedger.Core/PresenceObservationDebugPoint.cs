namespace PresenceLedger.Core;

/// <summary>One retained sample exposed in the day debug view.</summary>
public sealed record PresenceObservationDebugPoint(
    DateTimeOffset At,
    Novolis.Math.Geometry.GeoCoordinate? Position,
    double? AccuracyMeters,
    RecordedWifiStatus WifiStatus,
    string? ConnectedSsid);
