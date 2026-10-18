using System.Diagnostics;
using System.Threading.Channels;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using Novolis.Video;
using Novolis.Video.Codecs.H264;

namespace Novolis.Reach.Client.Windows;

/// <summary>Decodes the negotiated Windows H.264 stream for the Avalonia client.</summary>
public sealed class WindowsReachVideoPresenter :
    IReachVideoPresenter,
    IReachVideoPerformanceSource,
    IReachKeyFrameRequester,
    IReachVideoStreamResetter,
    IReachVideoDropSource
{
    private readonly Channel<ReachVideoFrame> _frames =
        Channel.CreateBounded<ReachVideoFrame>(
            new BoundedChannelOptions(3)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
    private readonly CancellationTokenSource _decodeCancellation = new();
    private readonly Task _decodeTask;
    private readonly object _gate = new();
    private WindowsH264Decoder? _decoder;
    private int _width;
    private int _height;
    private int _resetRequested;
    private int _disposeStarted;
    private int _keyFrameRequestSent;

    /// <summary>Creates the latest-frame Windows decoder worker.</summary>
    public WindowsReachVideoPresenter()
    {
        _decodeTask = DecodeLoopAsync(_decodeCancellation.Token);
    }

    /// <inheritdoc />
    public event Action<RawVideoFrame>? FrameDecoded;

    /// <inheritdoc />
    public event Action<double>? DecodeCompleted;

    /// <inheritdoc />
    public event Action? KeyFrameRequested;

    /// <inheritdoc />
    public event Action? FrameDropped;

    /// <inheritdoc />
    public void ResetStream()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            while (_frames.Reader.TryRead(out _))
            {
            }

            ResetDecoder();
            Interlocked.Exchange(ref _resetRequested, 0);
            Interlocked.Exchange(ref _keyFrameRequestSent, 0);
        }
    }

    /// <inheritdoc />
    public void Present(ReachVideoFrame frame)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeStarted) != 0,
            this);
        if (!string.Equals(frame.Codec, "H264", StringComparison.OrdinalIgnoreCase))
            return;

        if (_frames.Writer.TryWrite(frame))
            return;

        while (_frames.Reader.TryRead(out _))
        {
        }

        FrameDropped?.Invoke();
        Interlocked.Exchange(ref _resetRequested, 1);
        RequestKeyFrame();
        _frames.Writer.TryWrite(frame);
    }

    private async Task DecodeLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken))
            {
                if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
                    ResetStream();

                RawVideoFrame? decoded = null;
                var requestKeyFrame = false;
                var decodeStart = Stopwatch.GetTimestamp();
                lock (_gate)
                {
                    if (Volatile.Read(ref _disposeStarted) != 0)
                        return;

                    if (_decoder is null
                        || _width != frame.Width
                        || _height != frame.Height)
                    {
                        ResetDecoder();
                        _decoder = new WindowsH264Decoder(
                            frame.Width,
                            frame.Height);
                        _width = frame.Width;
                        _height = frame.Height;
                    }

                    try
                    {
                        decoded = _decoder.Decode(
                            new EncodedVideoFrame(
                                frame.Width,
                                frame.Height,
                                frame.Timestamp,
                                frame.AccessUnit,
                                frame.IsKeyFrame,
                                frame.Codec));
                        if (frame.IsKeyFrame)
                            Interlocked.Exchange(ref _keyFrameRequestSent, 0);
                    }
                    catch (InvalidOperationException)
                    {
                        // A delta frame can arrive before the decoder has a
                        // reference frame. Request an intra frame while
                        // keeping control/input independent of decoding.
                        if (frame.IsKeyFrame)
                            ResetDecoder();
                        requestKeyFrame =
                            Interlocked.Exchange(
                                ref _keyFrameRequestSent,
                                1) == 0;
                    }
                }

                if (requestKeyFrame)
                    KeyFrameRequested?.Invoke();
                if (decoded is null)
                    continue;

                DecodeCompleted?.Invoke(
                    Stopwatch.GetElapsedTime(decodeStart).TotalMilliseconds);
                FrameDecoded?.Invoke(decoded);
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
                ResetDecoder();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        _frames.Writer.TryComplete();
        _decodeCancellation.Cancel();
        try
        {
            _decodeTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _decodeCancellation.Dispose();
        FrameDecoded = null;
        DecodeCompleted = null;
        KeyFrameRequested = null;
        FrameDropped = null;
    }

    private void RequestKeyFrame()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
            return;

        if (Interlocked.Exchange(ref _keyFrameRequestSent, 1) == 0)
            KeyFrameRequested?.Invoke();
    }

    private void ResetDecoder()
    {
        _decoder?.Dispose();
        _decoder = null;
        _width = 0;
        _height = 0;
    }
}
