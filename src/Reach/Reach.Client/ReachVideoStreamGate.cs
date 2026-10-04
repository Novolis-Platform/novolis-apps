using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>
/// Coordinates key-frame recovery and stale decoded-frame suppression for a
/// remote video stream.
/// </summary>
public sealed class ReachVideoStreamGate
{
    private readonly VideoStreamGate _inner = new();

    /// <summary>Gets the current stream generation.</summary>
    public long Generation => _inner.Generation;

    /// <summary>
    /// Invalidates pending decoder work and requires the next accepted frame
    /// to be an intra frame.
    /// </summary>
    public void RequireKeyFrame() => _inner.RequireKeyFrame();

    /// <summary>
    /// Accepts a frame when it can safely begin or continue the stream.
    /// </summary>
    public bool TryAccept(
        ReachVideoFrame frame,
        out long generation)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return _inner.TryAccept(frame.IsKeyFrame, out generation);
    }

    /// <summary>Gets whether decoded work belongs to the current stream.</summary>
    public bool IsCurrent(long generation) =>
        _inner.IsCurrent(generation);
}
