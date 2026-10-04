using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
/// Runs in the interactive user session and owns capture and input access.
/// </summary>
public sealed class ReachSessionHost : BackgroundService
{
    private const string Endpoint = "Novolis.Reach.Host.Windows";
    internal readonly ILogger<ReachSessionHost> _log;
    internal readonly WindowsInputController _input;
    internal readonly WindowsClipboardService _clipboard;
    internal readonly WindowsDisplayTopology _display;
    internal readonly WindowsLoopbackAudioCapture _audioCapture;
    internal readonly SemaphoreSlim _sendGate = new(1, 1);
    internal readonly ReachPerformanceMetrics _performance = new();
    internal ILocalIpcConnection? _connection;
    internal WindowsDesktopCaptureSource? _capture;
    internal WindowsH264Encoder? _encoder;
    internal Channel<RawVideoFrame>? _frames;
    internal Task? _encodeTask;
    internal bool _audioStarted;
    internal bool _audioEnabled;
    internal bool _videoMetadataSent;
    internal string _selectedDisplayId = string.Empty;
    internal int _framesPerSecond = 30;
    internal int _targetWidth;
    internal int _targetHeight;
    internal int _targetBitrate = 8_000_000;
    internal int _streamWidth;
    internal int _streamHeight;
    internal long _sequence;
    internal Channel<LocalIpcFrame>? _videoQueue;
    internal Task? _videoSenderTask;

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
        Capture = new ReachSessionCapture(this);
        Encoder = new ReachSessionEncoder(this);
        Audio = new ReachSessionAudio(this);
        Control = new ReachSessionControl(this);
    }

    internal ReachSessionCapture Capture { get; }
    internal ReachSessionEncoder Encoder { get; }
    internal ReachSessionAudio Audio { get; }
    internal ReachSessionControl Control { get; }

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
            var videoQueue = Channel.CreateBounded<LocalIpcFrame>(
                new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                });
            _videoQueue = videoQueue;
            using var videoSenderCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _videoSenderTask = Encoder.VideoSendLoopAsync(
                connection,
                videoQueue.Reader,
                videoSenderCancellation.Token);
            try
            {
                await foreach (var frame in connection.ReadAllAsync(stoppingToken))
                {
                    if (!string.Equals(frame.Kind, "control", StringComparison.Ordinal))
                        continue;
                    await Control.HandleAsync(frame, stoppingToken).ConfigureAwait(false);
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
                videoQueue.Writer.TryComplete();
                videoSenderCancellation.Cancel();
                if (_videoSenderTask is not null)
                {
                    try
                    {
                        await _videoSenderTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (
                        videoSenderCancellation.IsCancellationRequested)
                    {
                    }
                }

                _videoSenderTask = null;
                _videoQueue = null;
                await Capture.StopAsync().ConfigureAwait(false);
                _connection = null;
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Capture.StopAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task SendAsync<T>(
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
}
