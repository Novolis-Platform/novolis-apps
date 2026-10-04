using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;
using Novolis.Video;
using Novolis.Video.Codecs.H264;

namespace Novolis.Reach.Host.Windows.Session;

internal sealed class ReachSessionEncoder(ReachSessionHost host)
{
    internal async Task EncodeLoopAsync(
        ILocalIpcConnection connection,
        ChannelReader<RawVideoFrame> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var captured in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var encodeStart = Stopwatch.GetTimestamp();
                var frame = ResizeFrame(captured);
                host._encoder ??= new WindowsH264Encoder(
                    frame.Width,
                    frame.Height,
                    host._framesPerSecond,
                    host._targetBitrate);
                if (!host._encoder.TryEncode(frame, out var encoded)
                    || encoded is null)
                {
                    continue;
                }

                host._performance.RecordEncoded(
                    Stopwatch.GetElapsedTime(encodeStart).TotalMilliseconds);
                host._streamWidth = encoded.Width;
                host._streamHeight = encoded.Height;
                if (!host._videoMetadataSent)
                {
                    host._videoMetadataSent = true;
                    await host.SendAsync(
                            connection,
                            ReachMessageType.VideoStreamStart,
                            new ReachVideoStreamStart(
                                encoded.Codec,
                                encoded.Width,
                                encoded.Height,
                                host._framesPerSecond),
                            "control",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var packet = new ReachVideoFrame(
                    Interlocked.Increment(ref host._sequence),
                    encoded.Width,
                    encoded.Height,
                    encoded.Timestamp,
                    encoded.Codec,
                    encoded.IsKeyFrame,
                    encoded.AccessUnit);
                var payload = ReachMessageCodec.Serialize(
                    ReachMessageType.VideoFrame,
                    Interlocked.Increment(ref host._sequence),
                    packet);
                QueueVideoFrame(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref host._sequence),
                        "media",
                        ReachMessageType.VideoFrame.ToString(),
                        payload));
            }
            catch (Exception exception)
            {
                host._log.LogWarning(exception, "Reach frame encoding failed.");
            }
        }
    }

    internal async Task VideoSendLoopAsync(
        ILocalIpcConnection connection,
        ChannelReader<LocalIpcFrame> reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await connection.SendAsync(frame, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is IOException
                        or ObjectDisposedException
                        or InvalidOperationException)
                {
                    host._log.LogDebug(exception, "Reach video IPC sender stopped.");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
    }

    internal void QueueVideoFrame(LocalIpcFrame frame)
    {
        var queue = Volatile.Read(ref host._videoQueue);
        if (queue is null)
        {
            host._performance.RecordDropped();
            return;
        }

        if (queue.Writer.TryWrite(frame))
            return;

        if (queue.Reader.TryRead(out _))
            host._performance.RecordDropped();
        if (!queue.Writer.TryWrite(frame))
            host._performance.RecordDropped();
    }

    internal void OnFrameCaptured(RawVideoFrame frame)
    {
        host._performance.RecordCaptured();
        host._frames?.Writer.TryWrite(frame);
    }

    private RawVideoFrame ResizeFrame(RawVideoFrame frame)
    {
        if (host._targetWidth <= 0
            || host._targetHeight <= 0
            || (host._targetWidth == frame.Width && host._targetHeight == frame.Height))
        {
            return frame;
        }

        var width = global::System.Math.Clamp(host._targetWidth, 1, 3840);
        var height = global::System.Math.Clamp(host._targetHeight, 1, 2160);
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var sourceY = y * frame.Height / height;
            for (var x = 0; x < width; x++)
            {
                var sourceX = x * frame.Width / width;
                var source = sourceY * frame.Stride + sourceX * 4;
                var destination = (y * width + x) * 4;
                frame.Pixels.AsSpan(source, 4).CopyTo(pixels.AsSpan(destination, 4));
            }
        }

        return new RawVideoFrame(
            width,
            height,
            width * 4,
            frame.Format,
            pixels,
            frame.Timestamp);
    }
}
