namespace Novolis.Reach.Protocol.Media;

/// <summary>Shared dimensions and pacing for Reach quality tiers.</summary>
public sealed record ReachVideoQualityProfile(
    ReachVideoQualityTier Tier,
    int Width,
    int Height,
    int FramesPerSecond,
    int TargetBitrate)
{
    /// <summary>Creates a quality profile for an announced display.</summary>
    public static ReachVideoQualityProfile ForSource(
        int sourceWidth,
        int sourceHeight,
        ReachVideoQualityTier tier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        var (maximumWidth, framesPerSecond, targetBitrate) = tier switch
        {
            ReachVideoQualityTier.Lan => (1440, 30, 8_000_000),
            ReachVideoQualityTier.Routed => (1080, 24, 5_000_000),
            _ => (720, 15, 3_000_000),
        };
        var scale = Math.Min(1d, maximumWidth / (double)sourceWidth);
        var width = Align(sourceWidth * scale);
        var height = Align(sourceHeight * scale);
        return new ReachVideoQualityProfile(
            tier,
            width,
            height,
            framesPerSecond,
            targetBitrate);
    }

    private static int Align(double value) =>
        Math.Max(16, (int)Math.Floor(value / 16d) * 16);
}
