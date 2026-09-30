using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Evidence summary for a durable event.</summary>
public sealed record PresenceEvidence
{
    /// <summary>Creates event evidence.</summary>
    public PresenceEvidence(PresenceConfidence confidence, TimeSpan confirmationDuration)
    {
        if (confirmationDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(confirmationDuration));

        Confidence = confidence;
        ConfirmationDuration = confirmationDuration;
    }

    /// <summary>Evidence confidence.</summary>
    public PresenceConfidence Confidence { get; }

    /// <summary>Duration satisfied before the event was emitted.</summary>
    public TimeSpan ConfirmationDuration { get; }
}
