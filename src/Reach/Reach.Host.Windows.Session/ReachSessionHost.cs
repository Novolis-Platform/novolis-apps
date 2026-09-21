using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Novolis.Reach.Protocol;
using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;
using Novolis.Video;
using Novolis.Video.Capture.Windows;
using Novolis.Video.Codecs.H264;
using Novolis.Windows.Audio;
using Novolis.Windows.Clipboard;
using Novolis.Windows.Display;
using Novolis.Windows.Input;

namespace Novolis.Reach.Host.Windows.Session;

/// <summary>
/// Headless helper that runs in the interactive user session and owns capture
/// and input access.
/// </summary>
public sealed class ReachSessionHost : BackgroundService
{
    private const string Endpoint = "Novolis.Reach.Host.Windows.Session";
    private readonly ILogger<ReachSessionHost> _log;
    private readonly WindowsInputController _input;
    private readonly WindowsClipboardService _clipboard;
    private readonly WindowsDisplayTopology _display;
    private readonly WindowsLoopbackAudioCapture _audioCapture;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private ILocalIpcConnection? _connection;
    private WindowsDesktopCaptureSource? _capture;
    private WindowsH264Encoder? _encoder;
    private Channel<RawVideoFrame>? _frames;
    private Task? _encodeTask;
    private bool _audioStarted;
    private long _sequence;

    /// <summary>Creates the interactive-session helper.</summary>
    public ReachSessionHost(
        ILogger<ReachSessionHost> log,
        WindowsInputController input,
        WindowsClipboardService clipboard,
        WindowsDisplayTopology display,
        WindowsLoopbackAudioCapture audioCapture)
    {
        _log = log;
        _input = input;
        _clipboard = clipboard;
        _display = display;
        _audioCapture = audioCapture;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var listener = LocalIpcTransport.CreateListener(
            new LocalIpcEndpoint(Endpoint));
        _log.LogInformation("Reach interactive session helper is listening.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var connection = await listener.AcceptAsync(stoppingToken)
                .ConfigureAwait(false);
            _connection = connection;
            try
            {
                await foreach (var frame in connection.ReadAllAsync(stoppingToken))
                {
                    if (!string.Equals(frame.Kind, "control", StringComparison.Ordinal))
                        continue;
                    await HandleControlAsync(frame, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogWarning(exception, "Reach service connection ended.");
            }
            finally
            {
                await StopCaptureAsync().ConfigureAwait(false);
                _connection = null;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopCaptureAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleControlAsync(
        LocalIpcFrame frame,
        CancellationToken cancellationToken)
    {
        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
        switch (envelope.Type)
        {
            case ReachMessageType.SessionOpen:
                await StartCaptureAsync(cancellationToken).ConfigureAwait(false);
                break;
            case ReachMessageType.SessionClose:
                await StopCaptureAsync().ConfigureAwait(false);
                break;
            case ReachMessageType.PointerMove:
            {
                var move = ReachMessageCodec.ReadBody<ReachPointerMove>(envelope);
                _input.MovePointer((int)Math.Round(move.X), (int)Math.Round(move.Y));
                break;
            }
            case ReachMessageType.PointerButton:
            {
                var button = ReachMessageCodec.ReadBody<ReachPointerButton>(envelope);
                if (button.IsDown
                    && Enum.TryParse<WindowsInputController.WindowsPointerButton>(
                        button.Button,
                        ignoreCase: true,
                        out var parsedButton))
                {
                    _input.Click(parsedButton, button.ClickCount);
                }

                break;
            }
            case ReachMessageType.PointerWheel:
                _input.Scroll(ReachMessageCodec.ReadBody<ReachPointerWheel>(envelope).Delta);
                break;
            case ReachMessageType.KeyDown:
                _input.Key(
                    ReachMessageCodec.ReadBody<ReachKeyEvent>(envelope).VirtualKey,
                    release: false);
                break;
            case ReachMessageType.KeyUp:
                _input.Key(
                    ReachMessageCodec.ReadBody<ReachKeyEvent>(envelope).VirtualKey,
                    release: true);
                break;
            case ReachMessageType.TextInput:
                _input.Text(ReachMessageCodec.ReadBody<ReachTextInput>(envelope).Text);
                break;
            case ReachMessageType.ClipboardContent:
            {
                var clipboard = ReachMessageCodec.ReadBody<ReachClipboardContent>(envelope);
                if (string.Equals(clipboard.Format, "text", StringComparison.OrdinalIgnoreCase)
                    && clipboard.Text is not null)
                {
                    _clipboard.WriteText(clipboard.Text);
                }
                else if (string.Equals(
                             clipboard.Format,
                             "files",
                             StringComparison.OrdinalIgnoreCase)
                         && clipboard.Files is not null)
                {
                    _clipboard.WriteFileDropList(clipboard.Files);
                }

                break;
            }
            case ReachMessageType.ClipboardChanged:
                await SendClipboardAsync(cancellationToken).ConfigureAwait(false);
                break;
            case ReachMessageType.VideoStreamConfiguration:
            {
                var configuration = ReachMessageCodec.ReadBody<ReachVideoStreamConfiguration>(envelope);
                await RestartCaptureAsync(
                        Math.Clamp(configuration.FramesPerSecond, 5, 60),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            case ReachMessageType.DisplayResize:
            {
                var resize = ReachMessageCodec.ReadBody<ReachDisplayResize>(envelope);
                await RestartCaptureAsync(
                        Math.Clamp(resize.FramesPerSecond, 5, 60),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            case ReachMessageType.RequestKeyFrame:
                await RestartCaptureAsync(30, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task StartCaptureAsync(CancellationToken cancellationToken)
    {
        if (_capture is not null)
            return;

        var connection = _connection
            ?? throw new InvalidOperationException("No service connection is available.");
        _frames = Channel.CreateBounded<RawVideoFrame>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });
        _capture = new WindowsDesktopCaptureSource(framesPerSecond: 30);
        _capture.FrameCaptured += OnFrameCaptured;
        await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
        _encodeTask = EncodeLoopAsync(connection, _frames.Reader, cancellationToken);

        await SendAsync(
                connection,
                ReachMessageType.VideoStreamStart,
                new ReachVideoStreamStart("H264", 0, 0, 30),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
        WaveFormat? audioFormat = null;
        try
        {
            _audioCapture.DataAvailable += OnAudioDataAvailable;
            _audioCapture.Start();
            _audioStarted = true;
            audioFormat = _audioCapture.Format;
        }
        catch (Exception exception)
        {
            _log.LogWarning(exception, "Loopback audio is unavailable; continuing without audio.");
            _audioCapture.DataAvailable -= OnAudioDataAvailable;
        }

        if (audioFormat is not null)
        {
            await SendAsync(
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
        await SendTopologyAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async Task RestartCaptureAsync(
        int framesPerSecond,
        CancellationToken cancellationToken)
    {
        await StopCaptureAsync().ConfigureAwait(false);
        if (_connection is not null)
        {
            _capture = new WindowsDesktopCaptureSource(framesPerSecond);
            _capture.FrameCaptured += OnFrameCaptured;
            _frames = Channel.CreateBounded<RawVideoFrame>(
                new BoundedChannelOptions(2)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true,
                });
            await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
            _encodeTask = EncodeLoopAsync(
                _connection,
                _frames.Reader,
                cancellationToken);
            try
            {
                _audioCapture.DataAvailable += OnAudioDataAvailable;
                _audioCapture.Start();
                _audioStarted = true;
            }
            catch (Exception exception)
            {
                _log.LogWarning(exception, "Loopback audio restart failed.");
                _audioCapture.DataAvailable -= OnAudioDataAvailable;
            }
        }
    }

    private async Task EncodeLoopAsync(
        ILocalIpcConnection connection,
        ChannelReader<RawVideoFrame> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var frame in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                _encoder ??= new WindowsH264Encoder(frame.Width, frame.Height);
                var encoded = _encoder.Encode(frame);
                var packet = new ReachVideoFrame(
                    Interlocked.Increment(ref _sequence),
                    encoded.Width,
                    encoded.Height,
                    encoded.Timestamp,
                    encoded.Codec,
                    encoded.IsKeyFrame,
                    encoded.AccessUnit);
                await SendAsync(
                        connection,
                        ReachMessageType.VideoFrame,
                        packet,
                        "media",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.LogWarning(exception, "Reach frame encoding failed.");
            }
        }
    }

    private async Task SendTopologyAsync(
        ILocalIpcConnection connection,
        CancellationToken cancellationToken)
    {
        var displays = _display.GetMonitors()
            .Select((monitor, index) => new ReachDisplay(
                $"display-{index}",
                monitor.Left,
                monitor.Top,
                monitor.Width,
                monitor.Height,
                monitor.Dpi))
            .ToArray();
        await SendAsync(
                connection,
                ReachMessageType.DisplayTopology,
                new ReachDisplayTopology(displays),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendClipboardAsync(CancellationToken cancellationToken)
    {
        var connection = _connection;
        if (connection is null)
            return;

        var files = _clipboard.ReadFileDropList();
        await SendAsync(
                connection,
                ReachMessageType.ClipboardContent,
                new ReachClipboardContent(
                    files.Count == 0 ? "text" : "files",
                    _clipboard.ReadText(),
                    files.ToArray()),
                "control",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendAsync<T>(
        ILocalIpcConnection connection,
        ReachMessageType type,
        T message,
        string kind,
        CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = ReachMessageCodec.Serialize(
                type,
                Interlocked.Increment(ref _sequence),
                message);
            await connection.SendAsync(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref _sequence),
                        kind,
                        type.ToString(),
                        payload),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private void OnFrameCaptured(RawVideoFrame frame)
    {
        _frames?.Writer.TryWrite(frame);
    }

    private void OnAudioDataAvailable(object? sender, NAudio.Wave.WaveInEventArgs args)
    {
        var connection = _connection;
        var format = _audioCapture.Format;
        if (connection is null || format is null || args.BytesRecorded == 0)
            return;

        var data = args.Buffer.AsSpan(0, args.BytesRecorded).ToArray();
        _ = SendAsync(
                connection,
                ReachMessageType.AudioFrame,
                new ReachAudioFrame(
                    Interlocked.Increment(ref _sequence),
                    DateTime.UtcNow.Ticks,
                    format.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat
                        ? "PCM_FLOAT"
                        : "PCM",
                    format.SampleRate,
                    format.Channels,
                    data,
                    format.BitsPerSample,
                    format.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat),
                "media",
                CancellationToken.None)
            .ContinueWith(
                task =>
                {
                    if (task.IsFaulted)
                        _log.LogDebug(task.Exception, "Audio frame send failed.");
                },
                TaskScheduler.Default);
    }

    private async Task StopCaptureAsync()
    {
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is not null)
        {
            capture.FrameCaptured -= OnFrameCaptured;
            await capture.StopAsync().ConfigureAwait(false);
            await capture.DisposeAsync().ConfigureAwait(false);
        }

        if (_audioStarted)
        {
            _audioCapture.DataAvailable -= OnAudioDataAvailable;
            _audioStarted = false;
            await _audioCapture.DisposeAsync().ConfigureAwait(false);
        }

        _frames?.Writer.TryComplete();
        _frames = null;
        var encodeTask = Interlocked.Exchange(ref _encodeTask, null);
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

        _encoder?.Dispose();
        _encoder = null;
    }
}
