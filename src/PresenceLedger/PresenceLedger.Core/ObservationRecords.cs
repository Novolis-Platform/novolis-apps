using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Wi-Fi capability captured alongside a local observation.</summary>
public enum RecordedWifiStatus
{
    /// <summary>The platform did not provide a Wi-Fi status.</summary>
    Unknown,

    /// <summary>The platform supplied a connected-network value.</summary>
    Available,

    /// <summary>The platform could not provide Wi-Fi because of permission or capability.</summary>
    Unavailable,

    /// <summary>The platform supplied a redacted value.</summary>
    Redacted,
}
