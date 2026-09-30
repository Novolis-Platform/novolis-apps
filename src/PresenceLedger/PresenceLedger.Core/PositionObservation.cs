using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>A location sample with reported horizontal uncertainty.</summary>
public sealed record PositionObservation : Observation
{
    /// <summary>Creates a position observation.</summary>
    public PositionObservation(
        DateTimeOffset at,
        GeoCoordinate position,
        double accuracyMeters)
        : base(at)
    {
        if (!double.IsFinite(accuracyMeters) || accuracyMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(accuracyMeters));

        Position = position;
        AccuracyMeters = accuracyMeters;
    }

    /// <summary>Observed position.</summary>
    public GeoCoordinate Position { get; }

    /// <summary>Reported horizontal accuracy in meters.</summary>
    public double AccuracyMeters { get; }
}
