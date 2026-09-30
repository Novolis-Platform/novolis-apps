using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>A transient platform observation.</summary>
public abstract record Observation(DateTimeOffset At);
