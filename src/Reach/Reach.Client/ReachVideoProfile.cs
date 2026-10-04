using Novolis.Reach.Protocol;
using Novolis.Video;

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
        return FromAdaptive(
            VideoAdaptiveProfile.ForSource(
                display.Width,
                display.Height,
                ToAdaptive(kind)));
    }

    internal static ReachVideoProfile FromAdaptive(VideoAdaptiveProfile profile) =>
        new(
            FromAdaptive(profile.Kind),
            profile.Width,
            profile.Height,
            profile.FramesPerSecond,
            profile.TargetBitrate);

    internal static VideoAdaptiveProfileKind ToAdaptive(ReachVideoProfileKind kind) =>
        kind switch
        {
            ReachVideoProfileKind.Lan => VideoAdaptiveProfileKind.High,
            ReachVideoProfileKind.Routed => VideoAdaptiveProfileKind.Medium,
            _ => VideoAdaptiveProfileKind.Low,
        };

    internal static ReachVideoProfileKind FromAdaptive(VideoAdaptiveProfileKind kind) =>
        kind switch
        {
            VideoAdaptiveProfileKind.High => ReachVideoProfileKind.Lan,
            VideoAdaptiveProfileKind.Medium => ReachVideoProfileKind.Routed,
            _ => ReachVideoProfileKind.Constrained,
        };
}
