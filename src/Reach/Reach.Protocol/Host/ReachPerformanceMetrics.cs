using Novolis.Video;

namespace Novolis.Reach.Protocol.Host;

/// <summary>
/// Keeps bounded Reach pipeline counters and maps them onto the wire snapshot.
/// </summary>
public sealed class ReachPerformanceMetrics
{
    private readonly VideoPipelineMetrics _inner = new();

    /// <summary>Records a frame observed by the capture source.</summary>
    public void RecordCaptured() => _inner.RecordCaptured();

    /// <summary>Records one encoded frame and its encode duration.</summary>
    public void RecordEncoded(double durationMilliseconds) =>
        _inner.RecordEncoded(durationMilliseconds);

    /// <summary>Records a frame sent by a transport boundary.</summary>
    public void RecordSent(int bytes) => _inner.RecordSent(bytes);

    /// <summary>Records a received frame and, when available, its source age.</summary>
    public void RecordReceived(int bytes, long? sourceUtcTicks = null) =>
        _inner.RecordReceived(bytes, sourceUtcTicks);

    /// <summary>Records one decoded frame and its decoder duration.</summary>
    public void RecordDecoded(double durationMilliseconds) =>
        _inner.RecordDecoded(durationMilliseconds);

    /// <summary>Records one presented frame and its presentation duration.</summary>
    public void RecordPresented(
        double durationMilliseconds,
        long? sourceUtcTicks = null) =>
        _inner.RecordPresented(durationMilliseconds, sourceUtcTicks);

    /// <summary>Records a frame evicted by a bounded media queue.</summary>
    public void RecordDropped() => _inner.RecordDropped();

    /// <summary>Records a request for a fresh intra frame.</summary>
    public void RecordKeyFrameRequest() => _inner.RecordKeyFrameRequest();

    /// <summary>Records the time spent waiting to send input.</summary>
    public void RecordInputRoundTrip(double durationMilliseconds) =>
        _inner.RecordInputRoundTrip(durationMilliseconds);

    /// <summary>Returns a Reach wire snapshot of the current pipeline.</summary>
    public ReachPerformanceSnapshot Snapshot()
    {
        var snapshot = _inner.Snapshot();
        return new ReachPerformanceSnapshot(
            snapshot.CapturedFrames,
            snapshot.EncodedFrames,
            snapshot.SentFrames,
            snapshot.ReceivedFrames,
            snapshot.PresentedFrames,
            snapshot.DroppedFrames,
            snapshot.KeyFrameRequests,
            snapshot.BytesSent,
            snapshot.BytesReceived,
            snapshot.FrameAgeP50Milliseconds,
            snapshot.FrameAgeP95Milliseconds,
            snapshot.EncodeP95Milliseconds,
            snapshot.DecodeP95Milliseconds,
            snapshot.PresentP95Milliseconds,
            snapshot.InputRoundTripP95Milliseconds,
            snapshot.LastFrameAt);
    }
}
