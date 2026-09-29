using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>Platform-specific decoder and presenter for Reach video frames.</summary>
public interface IReachVideoPresenter : IDisposable
{
    /// <summary>Raised after an encoded frame has been decoded.</summary>
    event Action<RawVideoFrame>? FrameDecoded;

    /// <summary>Accepts one encoded frame.</summary>
    void Present(ReachVideoFrame frame);
}

/// <summary>Optional decoder recovery notification for presenters that lose codec state.</summary>
public interface IReachKeyFrameRequester
{
    /// <summary>Raised when the presenter needs a fresh key frame.</summary>
    event Action? KeyFrameRequested;
}

/// <summary>Optional presenter hook for a host video-stream reset.</summary>
public interface IReachVideoStreamResetter
{
    /// <summary>Discards decoder state before the next key frame.</summary>
    void ResetStream();
}

/// <summary>Optional presenter diagnostics for decoder duration.</summary>
public interface IReachVideoPerformanceSource
{
    /// <summary>Raised after one encoded frame has been decoded.</summary>
    event Action<double>? DecodeCompleted;
}

/// <summary>Optional presenter diagnostics for dropped access units.</summary>
public interface IReachVideoDropSource
{
    /// <summary>Raised when a decoded access unit is evicted for freshness.</summary>
    event Action? FrameDropped;
}

/// <summary>Presenter used on platforms without a decoder in the current slice.</summary>
public sealed class NullReachVideoPresenter : IReachVideoPresenter
{
    /// <inheritdoc />
    public event Action<RawVideoFrame>? FrameDecoded
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Present(ReachVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
