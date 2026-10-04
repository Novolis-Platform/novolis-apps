using Novolis.Reach.Transport;
using Novolis.Transports;

namespace Novolis.Reach.Client;

/// <summary>Owns one client-side Reach control connection.</summary>
public sealed class ReachClientSession : IAsyncDisposable
{
    internal readonly int? _mediaPort;
    internal readonly SemaphoreSlim _sendGate = new(1, 1);
    internal readonly SemaphoreSlim _bulkSendGate = new(1, 1);
    internal readonly Guid _sessionId = Guid.NewGuid();
    internal readonly ReachPerformanceMetrics _performance = new();
    internal IReachTransportConnection? _transport;
    internal Stream? _stream;
    internal CancellationTokenSource? _receiveCancellation;
    internal Task? _receiveTask;
    internal Stream? _mediaStream;
    internal CancellationTokenSource? _mediaReceiveCancellation;
    internal Task? _mediaReceiveTask;
    internal ITransportDatagramChannel? _datagramChannel;
    internal ReachDatagramSession? _datagramSession;
    internal System.Net.IPEndPoint? _datagramEndpoint;
    internal CancellationTokenSource? _datagramReceiveCancellation;
    internal Task? _datagramReceiveTask;
    internal long _sequence;
    internal string? _lastEndpoint;
    internal ReachPlatform _platform;
    internal string? _clientName;
    internal long _lastVideoSequence;
    internal long _lastVideoFrameTicks;
    internal long _lastDatagramSequence = -1;
    internal int _disconnectRequested;
    internal int _remoteSessionEnded;
    internal int _state = (int)ReachClientConnectionState.Disconnected;
    internal CancellationTokenSource? _latencyCancellation;
    internal Task? _latencyTask;
    internal int _phase = (int)ReachConnectionPhase.Disconnected;

    /// <summary>Creates a Reach client session.</summary>
    public ReachClientSession(int? mediaPort = null)
    {
        if (mediaPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(mediaPort));

        _mediaPort = mediaPort;
        Inbound = new ReachClientInbound(this);
        Media = new ReachClientMediaChannel(this);
        Closer = new ReachClientCloser(this);
        Connector = new ReachClientConnector(this);
        Files = new ReachClientFileSender(this);
    }

    internal ReachClientInbound Inbound { get; }
    internal ReachClientMediaChannel Media { get; }
    internal ReachClientCloser Closer { get; }
    internal ReachClientConnector Connector { get; }
    internal ReachClientFileSender Files { get; }

    /// <summary>Raised when the session status changes.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Raised when the session state changes.</summary>
    public event Action<ReachClientConnectionState>? StateChanged;

    /// <summary>Raised when the remote host ends the session.</summary>
    public event Action<string>? SessionEnded;

    /// <summary>Raised when the media channel ends independently of control.</summary>
    public event Action? MediaConnectionLost;

    /// <summary>Raised when the control channel ends unexpectedly.</summary>
    public event Action? ConnectionLost;

    /// <summary>Raised for each encoded video frame received from the host.</summary>
    public event Action<ReachVideoFrame>? VideoFrameReceived;

    /// <summary>Raised when bounded performance diagnostics change.</summary>
    public event Action<ReachPerformanceSnapshot>? PerformanceChanged;

    /// <summary>Raised when the user-facing connection phase changes.</summary>
    public event Action<ReachConnectionPhase>? PhaseChanged;

    /// <summary>Raised when the host announces its display topology.</summary>
    public event Action<ReachDisplayTopology>? DisplayTopologyReceived;

    /// <summary>Raised when the host starts or restarts its video stream.</summary>
    public event Action<ReachVideoStreamStart>? VideoStreamStarted;

    /// <summary>Raised when the host resets its video stream.</summary>
    public event Action<ReachVideoStreamReset>? VideoStreamReset;

    /// <summary>Raised when the host starts its audio stream.</summary>
    public event Action<ReachAudioStreamStart>? AudioStreamStarted;

    /// <summary>Raised for each remote audio block received from the host.</summary>
    public event Action<ReachAudioFrame>? AudioFrameReceived;

    /// <summary>Raised when the host sends clipboard content.</summary>
    public event Action<ReachClipboardContent>? ClipboardContentReceived;

    /// <summary>Raised when the host pauses or resumes sharing.</summary>
    public event Action<ReachSharingState>? SharingStateChanged;

    /// <summary>Gets negotiated capabilities after connection.</summary>
    public ReachCapabilities? NegotiatedCapabilities { get; internal set; }

    /// <summary>Gets whether the control channel is connected.</summary>
    public bool IsConnected => _stream is not null;

    /// <summary>Gets whether the dedicated media channel is connected.</summary>
    public bool IsMediaConnected => _mediaStream is not null;

    /// <summary>Gets whether authenticated UDP media is connected.</summary>
    public bool IsDatagramConnected =>
        Volatile.Read(ref _datagramReceiveTask) is not null;

    /// <summary>Gets the current session state.</summary>
    public ReachClientConnectionState State =>
        (ReachClientConnectionState)Volatile.Read(ref _state);

    /// <summary>Gets the detailed user-facing connection phase.</summary>
    public ReachConnectionPhase Phase =>
        (ReachConnectionPhase)Volatile.Read(ref _phase);

    /// <summary>Gets the selected transport kind.</summary>
    public string ActiveTransport =>
        _transport?.Info.Kind.ToString() ?? "Disconnected";

    /// <summary>Gets the latest bounded performance snapshot.</summary>
    public ReachPerformanceSnapshot Performance => _performance.Snapshot();

    /// <summary>Gets the last received video sequence number.</summary>
    public long LastVideoSequence => Interlocked.Read(ref _lastVideoSequence);

    /// <summary>Gets when the last encoded video frame arrived.</summary>
    public DateTimeOffset? LastVideoFrameAt =>
        ReachClientSessionPerformance.LastVideoFrameAt(this);

    /// <summary>Records a frame after the UI presents it.</summary>
    public void RecordPresentedFrame(
        long sourceUtcTicks,
        double durationMilliseconds) =>
        ReachClientSessionPerformance.RecordPresented(this, sourceUtcTicks, durationMilliseconds);

    /// <summary>Records a decoder duration from a platform presenter.</summary>
    public void RecordDecodedFrame(double durationMilliseconds) =>
        ReachClientSessionPerformance.RecordDecoded(this, durationMilliseconds);

    /// <summary>Records a frame evicted by a platform decoder queue.</summary>
    public void RecordDroppedFrame() =>
        ReachClientSessionPerformance.RecordDropped(this);

    /// <summary>Connects and completes the Reach hello/capability exchange.</summary>
    public Task ConnectAsync(
        string endpoint,
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken = default) =>
        Connector.ConnectAsync(endpoint, platform, clientName, cancellationToken);

    /// <summary>Sends one typed control message.</summary>
    public Task SendAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken = default) =>
        ReachClientIO.SendAsync(this, type, message, cancellationToken);

    /// <summary>Disconnects the control channel.</summary>
    public Task DisconnectAsync() => Closer.DisconnectAsync();

    /// <summary>Reconnects to the last endpoint and requests session resume.</summary>
    public Task ReconnectAsync(CancellationToken cancellationToken = default) =>
        Connector.ReconnectAsync(cancellationToken);

    /// <summary>Reopens only the reliable media stream when control survives.</summary>
    public Task RecoverMediaAsync(CancellationToken cancellationToken = default) =>
        Connector.RecoverMediaAsync(cancellationToken);

    /// <summary>Sends a local file to the host in bounded chunks.</summary>
    public Task SendFileAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Files.SendAsync(path, cancellationToken);

    public Task SendClipboardTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.ClipboardText(this, text, cancellationToken);

    public Task SendPointerMoveAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.PointerMove(this, x, y, cancellationToken);

    public Task SendPointerButtonAsync(
        string button,
        bool isDown,
        int clickCount = 1,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.PointerButton(this, button, isDown, clickCount, cancellationToken);

    public Task SendPointerWheelAsync(
        int delta,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.PointerWheel(this, delta, cancellationToken);

    public Task SendKeyAsync(
        ushort virtualKey,
        bool isDown,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.Key(this, virtualKey, isDown, cancellationToken);

    public Task SendTextInputAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.Text(this, text, cancellationToken);

    public Task SendClipboardFilesAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.ClipboardFiles(this, files, cancellationToken);

    public Task ConfigureVideoAsync(
        int width,
        int height,
        int framesPerSecond,
        int targetBitrate,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.ConfigureVideo(
            this,
            width,
            height,
            framesPerSecond,
            targetBitrate,
            cancellationToken);

    public Task SelectDisplayAsync(
        string displayId,
        CancellationToken cancellationToken = default) =>
        ReachClientSends.SelectDisplay(this, displayId, cancellationToken);

    /// <summary>Requests a fresh intra frame after a decoder reset.</summary>
    public Task RequestKeyFrameAsync(
        CancellationToken cancellationToken = default) =>
        ReachClientSends.RequestKeyFrame(this, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendGate.Dispose();
        _bulkSendGate.Dispose();
    }

    internal void SetState(ReachClientConnectionState state)
    {
        if (Interlocked.Exchange(ref _state, (int)state) == (int)state)
            return;

        StateChanged?.Invoke(state);
    }

    internal void SetPhase(ReachConnectionPhase phase)
    {
        if (Interlocked.Exchange(ref _phase, (int)phase) == (int)phase)
            return;

        PhaseChanged?.Invoke(phase);
    }

    internal void RaiseStatus(string status) => StatusChanged?.Invoke(status);

    internal void RaisePerformanceChanged() =>
        PerformanceChanged?.Invoke(_performance.Snapshot());

    internal void RaiseMediaConnectionLost() => MediaConnectionLost?.Invoke();

    internal void RaiseConnectionLost() => ConnectionLost?.Invoke();

    internal void RaiseVideoStreamStarted(ReachVideoStreamStart value) => VideoStreamStarted?.Invoke(value);

    internal void RaiseVideoStreamReset(ReachVideoStreamReset value) => VideoStreamReset?.Invoke(value);

    internal void RaiseDisplayTopology(ReachDisplayTopology value) => DisplayTopologyReceived?.Invoke(value);

    internal void RaiseVideoFrame(ReachVideoFrame value) => VideoFrameReceived?.Invoke(value);

    internal void RaiseAudioStreamStarted(ReachAudioStreamStart value) => AudioStreamStarted?.Invoke(value);

    internal void RaiseAudioFrame(ReachAudioFrame value) => AudioFrameReceived?.Invoke(value);

    internal void RaiseSharingState(ReachSharingState value) => SharingStateChanged?.Invoke(value);

    internal void RaiseClipboardContent(ReachClipboardContent value) => ClipboardContentReceived?.Invoke(value);

    internal void RaiseSessionEnded(string reason) => SessionEnded?.Invoke(reason);
}
