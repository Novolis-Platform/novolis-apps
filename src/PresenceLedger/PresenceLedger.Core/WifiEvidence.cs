using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Configured Wi-Fi evidence for a location.</summary>
public sealed record WifiEvidence
{
    /// <summary>Creates Wi-Fi evidence for a display SSID.</summary>
    public WifiEvidence(string ssid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssid);
        Ssid = ssid.Trim();
    }

    /// <summary>Expected connected SSID.</summary>
    public string Ssid { get; }
}
