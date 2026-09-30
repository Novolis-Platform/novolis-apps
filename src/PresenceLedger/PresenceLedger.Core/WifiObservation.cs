using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>A connected-network observation; null means the platform had no SSID value.</summary>
public sealed record WifiObservation(
    DateTimeOffset At,
    string? ConnectedSsid)
    : Observation(At);
