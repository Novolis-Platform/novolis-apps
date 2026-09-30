using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Confidence attached to a durable presence event.</summary>
public enum PresenceConfidence
{
    /// <summary>Evidence satisfied the configured policy.</summary>
    Confirmed,
}
