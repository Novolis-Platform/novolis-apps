using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Semantic transition stored in the event ledger.</summary>
public enum PresenceTransition
{
    /// <summary>Presence was confirmed.</summary>
    Arrived,

    /// <summary>Departure was confirmed.</summary>
    Left,
}
