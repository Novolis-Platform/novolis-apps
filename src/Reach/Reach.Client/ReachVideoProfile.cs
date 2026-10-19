using Novolis.Reach.Protocol;

namespace Novolis.Reach.Client;

/// <summary>Negotiated dimensions and pacing for one video stream.</summary>
public sealed record ReachVideoProfile(
    ReachVideoProfileKind Kind,
    int Width,
    int Height,
    int FramesPerSecond,
    int TargetBitrate)
{
    /// <summary>Creates a conservative profile for an announced display.</summary>
    public static ReachVideoProfile ForDisplay(
        ReachDisplay display,
        ReachVideoProfileKind kind)
    {
        ArgumentNullException.ThrowIfNull(display);
        var profile = ReachVideoQualityProfile.ForSource(
            display.Width,
            display.Height,
            ToQualityTier(kind));
        return new ReachVideoProfile(
            kind,
            profile.Width,
            profile.Height,
            profile.FramesPerSecond,
            profile.TargetBitrate);
    }

    internal static ReachVideoProfileKind FromAdaptive(
        Novolis.Video.VideoAdaptiveProfileKind kind) =>
        kind switch
        {
            Novolis.Video.VideoAdaptiveProfileKind.High => ReachVideoProfileKind.Lan,
            Novolis.Video.VideoAdaptiveProfileKind.Medium => ReachVideoProfileKind.Routed,
            _ => ReachVideoProfileKind.Constrained,
        };

    internal static Novolis.Video.VideoAdaptiveProfileKind ToAdaptive(
        ReachVideoProfileKind kind) =>
        kind switch
        {
            ReachVideoProfileKind.Lan => Novolis.Video.VideoAdaptiveProfileKind.High,
            ReachVideoProfileKind.Routed => Novolis.Video.VideoAdaptiveProfileKind.Medium,
            _ => Novolis.Video.VideoAdaptiveProfileKind.Low,
        };

    private static ReachVideoQualityTier ToQualityTier(
        ReachVideoProfileKind kind) =>
        kind switch
        {
            ReachVideoProfileKind.Lan => ReachVideoQualityTier.Lan,
            ReachVideoProfileKind.Routed => ReachVideoQualityTier.Routed,
            _ => ReachVideoQualityTier.Constrained,
        };
}
