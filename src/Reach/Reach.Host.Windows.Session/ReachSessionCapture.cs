using System.Drawing;
using System.Threading.Channels;
using Novolis.Transports.LocalIpc;
using Novolis.Video;
using Novolis.Video.Capture.Windows;
using Novolis.Windows.Display;

namespace Novolis.Reach.Host.Windows.Session;

internal sealed class ReachSessionCapture(ReachSessionHost host)
{
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        if (host._capture is not null)
        {
            await SendMetadataAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var connection = host._connection
            ?? throw new InvalidOperationException("No service connection is available.");
        host._frames = Channel.CreateBounded<RawVideoFrame>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });
        host._videoMetadataSent = false;
        host._streamWidth = 0;
        host._streamHeight = 0;
        host._capture = CreateSource();
        host._capture.FrameCaptured += host.Encoder.OnFrameCaptured;
        await host._capture.StartAsync(cancellationToken).ConfigureAwait(false);
        host._encodeTask = host.Encoder.EncodeLoopAsync(
            connection,
            host._frames.Reader,
            cancellationToken);

        host.Audio.Start();
        await SendAudioMetadataAsync(connection, cancellationToken).ConfigureAwait(false);
        await SendTopologyAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    internal async Task RestartAsync(
        int width,
        int height,
        int framesPerSecond,
        int targetBitrate,
        CancellationToken cancellationToken,
        bool notifyReset = true)
    {
        host._targetWidth = width;
        host._targetHeight = height;
        host._framesPerSecond = framesPerSecond;
        host._targetBitrate = targetBitrate;
        await StopAsync().ConfigureAwait(false);
        var connection = host._connection;
        if (connection is not null)
        {
            if (notifyReset)
            {
                await host.SendAsync(
                        connection,
                        ReachMessageType.VideoStreamReset,
                        new ReachVideoStreamReset(Interlocked.Read(ref host._sequence)),
                        "control",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task StopAsync()
    {
        var capture = Interlocked.Exchange(ref host._capture, null);
        if (capture is not null)
        {
            capture.FrameCaptured -= host.Encoder.OnFrameCaptured;
            await capture.StopAsync().ConfigureAwait(false);
            await capture.DisposeAsync().ConfigureAwait(false);
        }

        await host.Audio.StopAsync().ConfigureAwait(false);
        host._frames?.Writer.TryComplete();
        host._frames = null;
        var encodeTask = Interlocked.Exchange(ref host._encodeTask, null);
        if (encodeTask is not null)
        {
            try
            {
                await encodeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        host._encoder?.Dispose();
        host._encoder = null;
        host._videoMetadataSent = false;
        host._streamWidth = 0;
        host._streamHeight = 0;
    }

    internal async Task SendMetadataAsync(CancellationToken cancellationToken)
    {
        var connection = host._connection;
        if (connection is null)
            return;

        if (host._streamWidth > 0 && host._streamHeight > 0)
        {
            await host.SendAsync(
                    connection,
                    ReachMessageType.VideoStreamStart,
                    new ReachVideoStreamStart(
                        "H264",
                        host._streamWidth,
                        host._streamHeight,
                        host._framesPerSecond),
                    "control",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        host.Audio.Start();
        await SendAudioMetadataAsync(connection, cancellationToken).ConfigureAwait(false);
        await SendTopologyAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAudioMetadataAsync(
        ILocalIpcConnection connection,
        CancellationToken cancellationToken)
    {
        if (!host._audioStarted || host._audioCapture.Format is not { } audioFormat)
            return;

        await host.SendAsync(
                connection,
                ReachMessageType.AudioStreamStart,
                new ReachAudioStreamStart(
                    audioFormat.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat
                        ? "PCM_FLOAT"
                        : "PCM",
                    audioFormat.SampleRate,
                    audioFormat.Channels,
                    audioFormat.BitsPerSample,
                    audioFormat.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendTopologyAsync(
        ILocalIpcConnection connection,
        CancellationToken cancellationToken)
    {
        var displays = host._display.GetMonitors()
            .Select((monitor, index) => new ReachDisplay(
                $"display-{index}",
                monitor.Left,
                monitor.Top,
                monitor.Width,
                monitor.Height,
                monitor.Dpi))
            .ToArray();
        await host.SendAsync(
                connection,
                ReachMessageType.DisplayTopology,
                new ReachDisplayTopology(displays),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private WindowsDesktopCaptureSource CreateSource() =>
        new(
            host._framesPerSecond,
            captureAllMonitors: GetSelectedMonitor() is null,
            captureBounds: GetSelectedMonitor() is { } monitor
                ? new Rectangle(
                    monitor.Left,
                    monitor.Top,
                    monitor.Width,
                    monitor.Height)
                : null,
            targetWidth: host._targetWidth,
            targetHeight: host._targetHeight);

    private WindowsMonitorInfo? GetSelectedMonitor()
    {
        if (!int.TryParse(
                host._selectedDisplayId.StartsWith("display-", StringComparison.Ordinal)
                    ? host._selectedDisplayId["display-".Length..]
                    : string.Empty,
                out var index))
        {
            return null;
        }

        return host._display.GetMonitors().ElementAtOrDefault(index);
    }
}
