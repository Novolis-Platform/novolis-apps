using Android.Media;
using Novolis.Reach.Client;
using Novolis.Reach.Protocol;
using Novolis.Video;

namespace Novolis.Reach.Client.Android;

/// <summary>Decodes Reach H.264 frames with the Android platform decoder.</summary>
public sealed class AndroidReachVideoPresenter : IReachVideoPresenter
{
    private const int FlexibleYuv420ColorFormat = unchecked((int)0x7F420888);
    private readonly object _gate = new();
    private MediaCodec? _codec;
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

        RawVideoFrame? decoded = null;
        lock (_gate)
        {
            try
            {
                EnsureDecoder(frame.Width, frame.Height);
                var codec = _codec;
                if (codec is null)
                    return;

                var inputIndex = codec.DequeueInputBuffer(10_000);
                if (inputIndex < 0)
                    return;

                var input = codec.GetInputBuffer(inputIndex);
                if (input is null || input.Capacity() < frame.AccessUnit.Length)
                    return;

                input.Clear();
                input.Put(frame.AccessUnit);
                codec.QueueInputBuffer(
                    inputIndex,
                    0,
                    frame.AccessUnit.Length,
                    frame.Timestamp / 1_000,
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
            catch (Exception)
            {
                ResetDecoder();
            }
        }

        if (decoded is not null)
            FrameDecoded?.Invoke(decoded);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            ResetDecoder();
        }

        FrameDecoded = null;
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

                var c = Math.Max(0, yValue - 16);
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

    private static int Clamp(int value) => Math.Clamp(value, 0, 255);

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
