using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>Normalizes decoded BGRA frame row orientation.</summary>
public static class ReachVideoFrameOrientation
{
    /// <summary>
    /// Flips a tightly packed BGRA frame vertically in place.
    /// </summary>
    public static void NormalizeTopToBottom(
        Span<byte> pixels,
        int width,
        int height) =>
        VideoFrameOrientation.NormalizeTopToBottom(pixels, width, height);
}
