using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>
/// Applies bounded quality changes with cooldowns so network pressure does not
/// cause visible profile oscillation.
/// </summary>
public sealed class ReachVideoProfileController
{
    private readonly VideoAdaptiveProfileController _inner;

    /// <summary>Creates a profile controller for one selected display.</summary>
    public ReachVideoProfileController(
        ReachDisplay display,
        ReachVideoProfileKind initialKind)
    {
        ArgumentNullException.ThrowIfNull(display);
        _inner = new VideoAdaptiveProfileController(
            display.Width,
            display.Height,
            ReachVideoProfile.ToAdaptive(initialKind));
    }

    /// <summary>Gets the profile currently applied to the host.</summary>
    public ReachVideoProfile Current =>
        ReachVideoProfile.FromAdaptive(_inner.Current);

    /// <summary>
    /// Returns a new profile only when pressure or sustained stability warrants
    /// a change.
    /// </summary>
    public ReachVideoProfile? Observe(
        ReachPerformanceSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var next = _inner.Observe(
            snapshot.DroppedFrames,
            snapshot.ReceivedFrames,
            snapshot.FrameAgeP95Milliseconds,
            snapshot.InputRoundTripP95Milliseconds,
            now);
        return next is null ? null : ReachVideoProfile.FromAdaptive(next);
    }
}
