using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Policy controlling confirmation, departure, and evidence continuity.</summary>
public sealed record PresencePolicy
{
    /// <summary>Creates a policy using the standard evidence gap.</summary>
    public PresencePolicy(
        TimeSpan confirmationDuration,
        TimeSpan departureDuration)
        : this(
            confirmationDuration,
            departureDuration,
            TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>Creates an inference policy.</summary>
    [JsonConstructor]
    public PresencePolicy(
        TimeSpan confirmationDuration,
        TimeSpan departureDuration,
        TimeSpan maximumEvidenceGap)
    {
        if (confirmationDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(confirmationDuration));

        if (departureDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(departureDuration));

        if (maximumEvidenceGap <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumEvidenceGap));

        ConfirmationDuration = confirmationDuration;
        DepartureDuration = departureDuration;
        MaximumEvidenceGap = maximumEvidenceGap;
    }

    /// <summary>Required sustained duration before arrival is confirmed.</summary>
    public TimeSpan ConfirmationDuration { get; }

    /// <summary>Required sustained duration before departure is confirmed.</summary>
    public TimeSpan DepartureDuration { get; }

    /// <summary>Maximum gap between qualifying observations in one evidence chain.</summary>
    public TimeSpan MaximumEvidenceGap { get; }
}
