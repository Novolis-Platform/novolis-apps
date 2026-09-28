using Android.Media;
using Android.Util;
using System.Threading.Channels;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client.Android;

/// <summary>Decodes Reach H.264 frames with the Android platform decoder.</summary>
public sealed class AndroidReachVideoPresenter :
    IReachVideoPresenter,
    IReachKeyFrameRequester,
    IReachVideoStreamResetter
{
    private const int FlexibleYuv420ColorFormat = unchecked((int)0x7F420888);
    private readonly Channel<ReachVideoFrame> _frames =
        Channel.CreateBounded<ReachVideoFrame>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
    private readonly CancellationTokenSource _decodeCancellation = new();
    private readonly Task _decodeTask;
    private MediaCodec? _codec;
    private int _width;
    private int _height;
    private readonly ReachVideoStreamGate _streamGate = new();
    private int _resetRequested;
    private int _keyFrameRequestSent;
    private int _disposeStarted;
    private int _decodedFramesLogged;
    private int _receivedFramesLogged;
    private int _rejectedFramesLogged;

    /// <summary>Creates the Android decoder and starts its latest-frame worker.</summary>
    public AndroidReachVideoPresenter()
    {
        _decodeTask = DecodeLoopAsync(_decodeCancellation.Token);
    }

    /// <inheritdoc />
    public event Action<RawVideoFrame>? FrameDecoded;

    /// <inheritdoc />
    public event Action? KeyFrameRequested;

    /// <inheritdoc />
    public void ResetStream()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
            return;

        _streamGate.RequireKeyFrame();
        Interlocked.Exchange(ref _resetRequested, 1);
        Interlocked.Exchange(ref _keyFrameRequestSent, 0);
        while (_frames.Reader.TryRead(out _))
        {
        }
    }

    /// <inheritdoc />
    public void Present(ReachVideoFrame frame)
    {
        if (Volatile.Read(ref _disposeStarted) != 0
            || !string.Equals(frame.Codec, "H264", StringComparison.OrdinalIgnoreCase))
            return;

        if (Interlocked.Increment(ref _receivedFramesLogged) <= 12)
        {
            Log.Info(
                "Novolis.Reach",
                $"Received H.264 frame {frame.Sequence}, key={frame.IsKeyFrame}, "
                + $"{frame.Width}x{frame.Height}, bytes={frame.AccessUnit.Length}.");
        }

        if (_frames.Writer.TryWrite(frame))
            return;

        // Never let slow software YUV conversion turn the TCP stream into a
        // queue of stale desktop frames. Drop the oldest access unit and ask
        // the host for a fresh intra frame so the decoder can catch up cleanly.
        _frames.Reader.TryRead(out _);
        _streamGate.RequireKeyFrame();
        RequestKeyFrame();
        _frames.Writer.TryWrite(frame);
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
        KeyFrameRequested = null;
    }

    private async Task DecodeLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken))
            {
                if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
                    ResetDecoder();

                if (!_streamGate.TryAccept(frame, out var generation))
                {
                    if (Interlocked.Increment(ref _rejectedFramesLogged) <= 12)
                    {
                        Log.Info(
                            "Novolis.Reach",
                            $"Rejected H.264 frame {frame.Sequence}, key={frame.IsKeyFrame}.");
                    }

                    RequestKeyFrame();
                    continue;
                }
                if (frame.IsKeyFrame)
                    Interlocked.Exchange(ref _keyFrameRequestSent, 0);
                RawVideoFrame? decoded = null;
                try
                {
                    EnsureDecoder(frame.Width, frame.Height);
                    var codec = _codec;
                    if (codec is null)
                        continue;

                    var inputIndex = codec.DequeueInputBuffer(10_000);
                    if (inputIndex < 0)
                        continue;

                    var input = codec.GetInputBuffer(inputIndex);
                    if (input is null || input.Capacity() < frame.AccessUnit.Length)
                        continue;

                    input.Clear();
                    input.Put(frame.AccessUnit);
                    codec.QueueInputBuffer(
                        inputIndex,
                        0,
                        frame.AccessUnit.Length,
                        frame.Timestamp / 10,
                        frame.IsKeyFrame
                            ? MediaCodecBufferFlags.KeyFrame
                            : MediaCodecBufferFlags.None);

                    var bufferInfo = new MediaCodec.BufferInfo();
                    while (true)
                    {
                        var outputIndex = codec.DequeueOutputBuffer(bufferInfo, 10_000);
                        if (outputIndex == (int)MediaCodecInfoState.TryAgainLater)
                            break;
                        if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged
                            || outputIndex == (int)MediaCodecInfoState.OutputBuffersChanged)
                        {
                            continue;
                        }
                        if (outputIndex < 0)
                            break;

                        try
                        {
                            using var image = codec.GetOutputImage(outputIndex);
                            if (image is not null && bufferInfo.Size > 0)
                            {
                                decoded = ConvertToBgra(
                                    image,
                                    frame.Width,
                                    frame.Height,
                                    frame.Timestamp);
                            }
                        }
                        finally
                        {
                            codec.ReleaseOutputBuffer(outputIndex, false);
                        }

                        break;
                    }
                }
                catch (Exception exception)
                {
                    Log.Warn(
                        "Novolis.Reach",
                        $"H.264 decoder reset: {exception.GetType().Name}: {exception.Message}");
                    ResetDecoder();
                    _streamGate.RequireKeyFrame();
                    RequestKeyFrame(force: true);
                }

                if (decoded is not null
                    && _streamGate.IsCurrent(generation))
                {
                    if (Interlocked.Increment(ref _decodedFramesLogged) <= 5)
                    {
                        Log.Info(
                            "Novolis.Reach",
                            $"Decoded H.264 frame {decoded.Width}x{decoded.Height}, "
                            + $"min={decoded.Pixels.Min()}, max={decoded.Pixels.Max()}.");
                    }

                    FrameDecoded?.Invoke(decoded);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            ResetDecoder();
        }
    }

    private void RequestKeyFrame(bool force = false)
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
            return;

        if (force)
            Interlocked.Exchange(ref _keyFrameRequestSent, 0);
        if (Interlocked.Exchange(ref _keyFrameRequestSent, 1) == 0)
            KeyFrameRequested?.Invoke();
    }

    private void EnsureDecoder(int width, int height)
    {
        if (_codec is not null && _width == width && _height == height)
            return;

        ResetDecoder();
        var format = MediaFormat.CreateVideoFormat(
            MediaFormat.MimetypeVideoAvc,
            width,
            height);
        format.SetInteger(
            MediaFormat.KeyColorFormat,
            FlexibleYuv420ColorFormat);
        _codec = MediaCodec.CreateDecoderByType(MediaFormat.MimetypeVideoAvc);
        _codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        _codec.Start();
        _width = width;
        _height = height;
    }

    private void ResetDecoder()
    {
        var codec = Interlocked.Exchange(ref _codec, null);
        if (codec is null)
            return;

        try
        {
            codec.Stop();
        }
        catch (Exception)
        {
        }

        try
        {
            codec.Release();
        }
        catch (Exception)
        {
        }

        codec.Dispose();
        _width = 0;
        _height = 0;
    }

    private static RawVideoFrame? ConvertToBgra(
        Image image,
        int width,
        int height,
        long timestamp)
    {
        var planes = image.GetPlanes();
        if (planes is null || planes.Length < 3)
            return null;

        var yPlane = CopyPlane(planes[0]);
        var uPlane = CopyPlane(planes[1]);
        var vPlane = CopyPlane(planes[2]);
        var pixels = new byte[checked(width * height * 4)];

        for (var y = 0; y < height; y++)
        {
            var chromaY = y / 2;
            for (var x = 0; x < width; x++)
            {
                var chromaX = x / 2;
                var yValue = yPlane.Read(y, x);
                var uValue = uPlane.Read(chromaY, chromaX);
                var vValue = vPlane.Read(chromaY, chromaX);

                var c = global::System.Math.Max(0, yValue - 16);
                var red = Clamp((298 * c + 409 * (vValue - 128) + 128) >> 8);
                var green = Clamp(
                    (298 * c
                     - 100 * (uValue - 128)
                     - 208 * (vValue - 128)
                     + 128) >> 8);
                var blue = Clamp((298 * c + 516 * (uValue - 128) + 128) >> 8);

                var pixel = (y * width + x) * 4;
                pixels[pixel] = (byte)blue;
                pixels[pixel + 1] = (byte)green;
                pixels[pixel + 2] = (byte)red;
                pixels[pixel + 3] = byte.MaxValue;
            }
        }

        return new RawVideoFrame(
            width,
            height,
            checked(width * 4),
            VideoPixelFormat.Bgra32,
            pixels,
            timestamp);
    }

    private static PlaneData CopyPlane(Image.Plane plane)
    {
        var buffer = plane.Buffer
            ?? throw new InvalidOperationException("Android decoder returned an empty image plane.");
        using var source = buffer.Duplicate();
        var bytes = new byte[source.Remaining()];
        source.Get(bytes);
        return new PlaneData(bytes, plane.RowStride, plane.PixelStride);
    }

    private static int Clamp(int value) => global::System.Math.Clamp(value, 0, 255);

    private readonly record struct PlaneData(
        byte[] Bytes,
        int RowStride,
        int PixelStride)
    {
        public int Read(int row, int column)
        {
            var index = row * RowStride + column * PixelStride;
            return index >= 0 && index < Bytes.Length
                ? Bytes[index]
                : 128;
        }
    }
}
