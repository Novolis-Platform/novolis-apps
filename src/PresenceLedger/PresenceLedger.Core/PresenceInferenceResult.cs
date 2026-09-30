using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Result of applying one observation to one location.</summary>
public sealed record PresenceInferenceResult(
    LocationPresenceState State,
    PresenceEvent? Event);
