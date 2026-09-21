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
