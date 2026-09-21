using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using Novolis.Video;
using Novolis.Video.Codecs.H264;

namespace Novolis.Reach.Client.Windows;

/// <summary>Decodes the negotiated Windows H.264 stream for the Avalonia client.</summary>
public sealed class WindowsReachVideoPresenter : IReachVideoPresenter
{
    private readonly object _gate = new();
    private WindowsH264Decoder? _decoder;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <inheritdoc />
    public event Action<RawVideoFrame>? FrameDecoded;

    /// <inheritdoc />
    public void Present(ReachVideoFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!string.Equals(frame.Codec, "H264", StringComparison.OrdinalIgnoreCase))
            return;

        RawVideoFrame decoded;
        lock (_gate)
        {
            if (_decoder is null || _width != frame.Width || _height != frame.Height)
            {
                _decoder?.Dispose();
                _decoder = new WindowsH264Decoder(frame.Width, frame.Height);
                _width = frame.Width;
                _height = frame.Height;
            }

            decoded = _decoder.Decode(
                new EncodedVideoFrame(
                    frame.Width,
                    frame.Height,
                    frame.Timestamp,
                    frame.AccessUnit,
                    frame.IsKeyFrame,
                    frame.Codec));
        }

        FrameDecoded?.Invoke(decoded);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            _disposed = true;
            _decoder?.Dispose();
            _decoder = null;
        }

        FrameDecoded = null;
    }
}
