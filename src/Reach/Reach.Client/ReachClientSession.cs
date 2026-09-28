using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Novolis.Reach.Protocol;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Client;

/// <summary>Describes the observable state of a Reach client session.</summary>
public enum ReachSessionState
{
    Disconnected,
    Connecting,
    Connected,
    Streaming,
    Lost,
}

/// <summary>Owns one client-side Reach control connection.</summary>
public sealed class ReachClientSession : IAsyncDisposable
{
    private readonly int? _mediaPort;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private TcpClient? _mediaClient;
    private NetworkStream? _mediaStream;
    private CancellationTokenSource? _mediaReceiveCancellation;
    private Task? _mediaReceiveTask;
    private long _sequence;
    private string? _lastEndpoint;
    private ReachPlatform _platform;
    private string? _clientName;
    private readonly Guid _sessionId = Guid.NewGuid();
    private long _lastVideoSequence;
    private long _lastVideoFrameTicks;
    private int _disconnectRequested;
    private int _state = (int)ReachSessionState.Disconnected;

    /// <summary>Creates a Reach client session.</summary>
    /// <param name="mediaPort">
    /// Optional media port override. Use zero to disable the dedicated media
    /// channel, which is useful for control-only peers.
    /// </param>
    public ReachClientSession(int? mediaPort = null)
    {
        if (mediaPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(mediaPort));

        _mediaPort = mediaPort;
    }

    /// <summary>Raised when the session status changes.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Raised when the session state changes.</summary>
    public event Action<ReachSessionState>? StateChanged;

    /// <summary>Raised when the remote host ends the session.</summary>
    public event Action<string>? SessionEnded;

    /// <summary>Raised when the media channel ends independently of control.</summary>
    public event Action? MediaConnectionLost;

    /// <summary>Raised when the control channel ends unexpectedly.</summary>
    public event Action? ConnectionLost;

    /// <summary>Raised for each encoded video frame received from the host.</summary>
    public event Action<ReachVideoFrame>? VideoFrameReceived;

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

    /// <summary>Gets negotiated capabilities after connection.</summary>
    public ReachCapabilities? NegotiatedCapabilities { get; private set; }

    /// <summary>Gets whether the control channel is connected.</summary>
    public bool IsConnected => _stream is not null;

    /// <summary>Gets whether the dedicated media channel is connected.</summary>
    public bool IsMediaConnected => _mediaStream is not null;

    /// <summary>Gets the current session state.</summary>
    public ReachSessionState State =>
        (ReachSessionState)Volatile.Read(ref _state);

    /// <summary>Gets the last received video sequence number.</summary>
    public long LastVideoSequence => Interlocked.Read(ref _lastVideoSequence);

    /// <summary>Gets when the last encoded video frame arrived.</summary>
    public DateTimeOffset? LastVideoFrameAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastVideoFrameTicks);
            return ticks == 0
                ? null
                : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>Connects and completes the Reach hello/capability exchange.</summary>
    public async Task ConnectAsync(
        string endpoint,
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        var address = ParseEndpoint(endpoint);
        Volatile.Write(ref _disconnectRequested, 0);
        SetState(ReachSessionState.Connecting);
        _lastEndpoint = endpoint;
        _platform = platform;
        _clientName = clientName;
        var client = new TcpClient(address.AddressFamily);
        RaiseStatus($"Connecting to {address.Address}:{address.Port}...");
        try
        {
            await client.ConnectAsync(address.Address, address.Port, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            SetState(ReachSessionState.Lost);
            RaiseStatus($"Unable to connect to {address.Address}:{address.Port}.");
            throw;
        }

        var stream = client.GetStream();
        _client = client;
        _stream = stream;
        SetState(ReachSessionState.Connected);
        RaiseStatus($"Connected to {address.Address}:{address.Port}; negotiating...");

        try
        {
            await SendAsync(
                ReachMessageType.ClientHello,
                new ReachClientHello(
                    ReachProtocol.AppId,
                    ReachProtocol.Version,
                    platform,
                    clientName),
                cancellationToken).ConfigureAwait(false);
            await SendAsync(
                ReachMessageType.ClientCapabilities,
                new ReachCapabilitiesMessage(GetCapabilities(platform)),
                cancellationToken).ConfigureAwait(false);

            RaiseStatus("Waiting for Reach host capabilities...");
            var hostHello = await ReadAsync<ReachHostHello>(
                    ReachMessageType.HostHello,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(hostHello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hostHello.ProtocolVersion))
            {
                throw new InvalidDataException("The remote host does not speak Reach protocol 1.x.");
            }

            var hostCapabilities = await ReadAsync<ReachCapabilitiesMessage>(
                    ReachMessageType.HostCapabilities,
                    cancellationToken)
                .ConfigureAwait(false);
            var negotiatedCapabilities = ReachCapabilities.Intersect(
                hostCapabilities.Capabilities,
                GetCapabilities(platform));
            NegotiatedCapabilities = negotiatedCapabilities;
            if (_lastVideoSequence == 0)
            {
                await SendAsync(
                        ReachMessageType.SessionOpen,
                        new ReachSessionOpen(
                            _sessionId,
                            string.Empty,
                            negotiatedCapabilities.Supports(ReachCapability.Audio)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendAsync(
                        ReachMessageType.SessionResume,
                        new ReachSessionResume(
                            _sessionId,
                            _lastVideoSequence,
                            negotiatedCapabilities.Supports(ReachCapability.Audio)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            _receiveCancellation = new CancellationTokenSource();
            var receiveCancellation = _receiveCancellation;
            _receiveTask = Task.Run(
                () => ReceiveLoopAsync(
                    receiveCancellation,
                    receiveCancellation.Token),
                CancellationToken.None);
            await TryConnectMediaAsync(address, cancellationToken).ConfigureAwait(false);
            if (_mediaPort != 0 && !IsMediaConnected)
            {
                RaiseStatus(
                    $"Connected to {hostHello.HostName}, but the video channel is unavailable.");
                MediaConnectionLost?.Invoke();
            }

            // Publish the connected state before requesting the first frame.
            // The receive loop can deliver that frame before this method
            // returns, so a trailing "connected" status would overwrite the
            // more specific streaming status in the client surface.
            RaiseStatus(
                IsMediaConnected || _mediaPort == 0
                    ? $"Connected to {hostHello.HostName}; "
                      + $"video={string.Join(",", negotiatedCapabilities.OfferedVideoCodecs)}"
                    : $"Connected to {hostHello.HostName}, but the video channel is unavailable.");
            await RequestKeyFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CloseTransportAsync(announce: false).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Sends one typed control message.</summary>
    public async Task SendAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken = default)
    {
        var stream = _stream ?? throw new InvalidOperationException("Reach is not connected.");
        var payload = ReachMessageCodec.Serialize(
            type,
            Interlocked.Increment(ref _sequence),
            message);
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LengthPrefixedFrameCodec.WriteAsync(stream, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>Disconnects the control channel.</summary>
    public async Task DisconnectAsync()
    {
        Volatile.Write(ref _disconnectRequested, 1);
        if (IsConnected)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await SendAsync(
                        ReachMessageType.SessionClose,
                        new ReachSessionClose(_sessionId, "Client disconnected."),
                        timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The peer may already have closed the connection.
            }
        }

        await CloseTransportAsync(announce: true).ConfigureAwait(false);
    }

    /// <summary>Reconnects to the last endpoint and requests session resume.</summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_lastEndpoint is null || _clientName is null)
            throw new InvalidOperationException("No previous Reach endpoint is available.");

        Volatile.Write(ref _disconnectRequested, 0);
        await CloseTransportAsync(announce: true).ConfigureAwait(false);
        await ConnectAsync(
                _lastEndpoint,
                _platform,
                _clientName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sends a local file to the host in bounded chunks.</summary>
    public async Task SendFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("The file to transfer does not exist.", path);

        var hash = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
        var transferId = Guid.NewGuid();
        await SendAsync(
                ReachMessageType.FileOffer,
                new ReachFileOffer(
                    transferId,
                    Path.GetFileName(path),
                    fileInfo.Length,
                    Convert.ToHexString(hash)),
                cancellationToken)
            .ConfigureAwait(false);

        await using var file = File.OpenRead(path);
        var buffer = new byte[64 * 1024];
        long offset = 0;
        while (true)
        {
            var read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            await SendAsync(
                    ReachMessageType.FileChunk,
                    new ReachFileChunk(
                        transferId,
                        offset,
                        buffer.AsSpan(0, read).ToArray()),
                    cancellationToken)
                .ConfigureAwait(false);
            offset += read;
        }

        await SendAsync(
                ReachMessageType.FileComplete,
                new ReachFileComplete(transferId, true, null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sends text to the host clipboard.</summary>
    public Task SendClipboardTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.ClipboardContent,
            new ReachClipboardContent("text", text),
            cancellationToken);

    /// <summary>Sends one pointer position in the selected display.</summary>
    public Task SendPointerMoveAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.PointerMove,
            new ReachPointerMove(x, y),
            cancellationToken);

    /// <summary>Sends one pointer button transition.</summary>
    public Task SendPointerButtonAsync(
        string button,
        bool isDown,
        int clickCount = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.PointerButton,
            new ReachPointerButton(button, isDown, clickCount),
            cancellationToken);

    /// <summary>Sends one pointer wheel delta.</summary>
    public Task SendPointerWheelAsync(
        int delta,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.PointerWheel,
            new ReachPointerWheel(delta),
            cancellationToken);

    /// <summary>Sends one virtual-key transition.</summary>
    public Task SendKeyAsync(
        ushort virtualKey,
        bool isDown,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            isDown ? ReachMessageType.KeyDown : ReachMessageType.KeyUp,
            new ReachKeyEvent(virtualKey),
            cancellationToken);

    /// <summary>Sends Unicode text input to the host.</summary>
    public Task SendTextInputAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.TextInput,
            new ReachTextInput(text),
            cancellationToken);

    /// <summary>Sends file paths as a rich clipboard file-list payload.</summary>
    public Task SendClipboardFilesAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        return SendAsync(
            ReachMessageType.ClipboardContent,
            new ReachClipboardContent("files", null, files.ToArray()),
            cancellationToken);
    }

    /// <summary>Requests a display and adaptive stream configuration.</summary>
    public Task ConfigureVideoAsync(
        int width,
        int height,
        int framesPerSecond,
        int targetBitrate,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.VideoStreamConfiguration,
            new ReachVideoStreamConfiguration(
                "H264",
                width,
                height,
                framesPerSecond,
                targetBitrate),
            cancellationToken);

    /// <summary>Selects a monitor announced by the host.</summary>
    public Task SelectDisplayAsync(
        string displayId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.DisplaySelect,
            new ReachDisplaySelect(displayId),
            cancellationToken);

    /// <summary>Requests a fresh intra frame after a decoder reset.</summary>
    public Task RequestKeyFrameAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ReachMessageType.RequestKeyFrame,
            new ReachRequestKeyFrame(_lastVideoSequence),
            cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendGate.Dispose();
    }

    private async Task<T> ReadAsync<T>(
        ReachMessageType expectedType,
        CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("Reach is not connected.");
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach host closed the control channel.");
        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
        return ReachMessageCodec.ReadBody<T>(envelope, expectedType);
    }

    private async Task ReceiveLoopAsync(
        CancellationTokenSource owner,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = _stream;
                if (stream is null)
                    return;

                var frame = await LengthPrefixedFrameCodec.ReadAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (frame is null)
                    return;

                if (!ProcessIncomingEnvelope(
                        ReachMessageCodec.Deserialize(frame.Payload)))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RaiseStatus($"Receive failed: {exception.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested
                && Volatile.Read(ref _disconnectRequested) == 0)
            {
                HandleUnexpectedDisconnect(owner);
            }
        }
    }

    private bool ProcessIncomingEnvelope(ReachMessageEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case ReachMessageType.VideoStreamStart:
                VideoStreamStarted?.Invoke(
                    ReachMessageCodec.ReadBody<ReachVideoStreamStart>(envelope));
                break;
            case ReachMessageType.VideoStreamReset:
            {
                var reset = ReachMessageCodec.ReadBody<ReachVideoStreamReset>(envelope);
                Interlocked.Exchange(ref _lastVideoSequence, reset.Sequence);
                VideoStreamReset?.Invoke(reset);
                break;
            }
            case ReachMessageType.DisplayTopology:
                DisplayTopologyReceived?.Invoke(
                    ReachMessageCodec.ReadBody<ReachDisplayTopology>(envelope));
                break;
            case ReachMessageType.VideoFrame:
            {
                var video = ReachMessageCodec.ReadBody<ReachVideoFrame>(envelope);
                Interlocked.Exchange(ref _lastVideoSequence, video.Sequence);
                Interlocked.Exchange(
                    ref _lastVideoFrameTicks,
                    DateTimeOffset.UtcNow.UtcTicks);
                SetState(ReachSessionState.Streaming);
                VideoFrameReceived?.Invoke(video);
                break;
            }
            case ReachMessageType.AudioStreamStart:
                AudioStreamStarted?.Invoke(
                    ReachMessageCodec.ReadBody<ReachAudioStreamStart>(envelope));
                break;
            case ReachMessageType.AudioFrame:
                AudioFrameReceived?.Invoke(
                    ReachMessageCodec.ReadBody<ReachAudioFrame>(envelope));
                break;
            case ReachMessageType.ClipboardContent:
                ClipboardContentReceived?.Invoke(
                    ReachMessageCodec.ReadBody<ReachClipboardContent>(envelope));
                break;
            case ReachMessageType.SessionClose:
            {
                var close = ReachMessageCodec.ReadBody<ReachSessionClose>(envelope);
                RaiseStatus($"The Reach host closed the session: {close.Reason}");
                SessionEnded?.Invoke(close.Reason);
                return false;
            }
        }

        return true;
    }

    private async Task TryConnectMediaAsync(
        IPEndPoint controlAddress,
        CancellationToken cancellationToken)
    {
        if (_mediaPort == 0)
            return;

        var client = new TcpClient(controlAddress.AddressFamily);
        try
        {
            var mediaPort = _mediaPort
                ?? (controlAddress.Port == ReachProtocol.ControlPort
                    ? ReachProtocol.MediaPort
                    : checked(controlAddress.Port + 1));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            await client.ConnectAsync(
                    controlAddress.Address,
                    mediaPort,
                    timeout.Token)
                .ConfigureAwait(false);
            var stream = client.GetStream();
            await LengthPrefixedFrameCodec.WriteAsync(
                    stream,
                    ReachMessageCodec.Serialize(
                        ReachMessageType.MediaHello,
                        Interlocked.Increment(ref _sequence),
                        new ReachMediaHello(
                            _sessionId,
                            ReachProtocol.AppId,
                            ReachProtocol.Version)),
                    timeout.Token)
                .ConfigureAwait(false);

            _mediaClient = client;
            _mediaStream = stream;
            _mediaReceiveCancellation = new CancellationTokenSource();
            var receiveCancellation = _mediaReceiveCancellation;
            _mediaReceiveTask = Task.Run(
                () => ReceiveMediaLoopAsync(
                    receiveCancellation,
                    receiveCancellation.Token),
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
        }
        catch (SocketException)
        {
            client.Dispose();
        }
        catch (IOException)
        {
            client.Dispose();
        }
    }

    private async Task ReceiveMediaLoopAsync(
        CancellationTokenSource owner,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = _mediaStream;
                if (stream is null)
                    return;

                var frame = await LengthPrefixedFrameCodec.ReadAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (frame is null)
                    return;

                if (!ProcessIncomingEnvelope(
                        ReachMessageCodec.Deserialize(frame.Payload)))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RaiseStatus($"Media receive failed: {exception.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                DetachMediaTransport(owner);
                if (IsConnected)
                    SetState(ReachSessionState.Connected);
                RaiseStatus("The Reach media stream ended.");
                MediaConnectionLost?.Invoke();
            }
        }
    }

    private void DetachMediaTransport(CancellationTokenSource owner)
    {
        if (!ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _mediaReceiveCancellation,
                    null,
                    owner),
                owner))
        {
            return;
        }

        Interlocked.Exchange(ref _mediaReceiveTask, null);
        Interlocked.Exchange(ref _mediaStream, null)?.Dispose();
        Interlocked.Exchange(ref _mediaClient, null)?.Dispose();
        owner.Dispose();
    }

    private async Task CloseTransportAsync(bool announce)
    {
        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);
        var receiveCancellation = Interlocked.Exchange(ref _receiveCancellation, null);
        var receiveTask = Interlocked.Exchange(ref _receiveTask, null);
        receiveCancellation?.Cancel();
        if (stream is not null)
            await stream.DisposeAsync().ConfigureAwait(false);
        client?.Dispose();
        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                receiveCancellation?.IsCancellationRequested == true)
            {
            }
        }

        receiveCancellation?.Dispose();
        var mediaStream = Interlocked.Exchange(ref _mediaStream, null);
        var mediaClient = Interlocked.Exchange(ref _mediaClient, null);
        var mediaCancellation = Interlocked.Exchange(
            ref _mediaReceiveCancellation,
            null);
        var mediaTask = Interlocked.Exchange(ref _mediaReceiveTask, null);
        mediaCancellation?.Cancel();
        if (mediaStream is not null)
            await mediaStream.DisposeAsync().ConfigureAwait(false);
        mediaClient?.Dispose();
        if (mediaTask is not null)
        {
            try
            {
                await mediaTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                mediaCancellation?.IsCancellationRequested == true)
            {
            }
        }

        mediaCancellation?.Dispose();
        NegotiatedCapabilities = null;
        Interlocked.Exchange(ref _lastVideoSequence, 0);
        Interlocked.Exchange(ref _lastVideoFrameTicks, 0);
        SetState(ReachSessionState.Disconnected);
        if (announce)
            RaiseStatus("Disconnected");
    }

    private void HandleUnexpectedDisconnect(CancellationTokenSource owner)
    {
        if (!ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _receiveCancellation,
                    null,
                    owner),
                owner))
        {
            return;
        }

        Interlocked.Exchange(ref _receiveTask, null);
        Interlocked.Exchange(ref _stream, null)?.Dispose();
        Interlocked.Exchange(ref _client, null)?.Dispose();
        var mediaOwner = Interlocked.Exchange(
            ref _mediaReceiveCancellation,
            null);
        mediaOwner?.Cancel();
        Interlocked.Exchange(ref _mediaStream, null)?.Dispose();
        Interlocked.Exchange(ref _mediaClient, null)?.Dispose();
        NegotiatedCapabilities = null;
        Interlocked.Exchange(ref _lastVideoSequence, 0);
        Interlocked.Exchange(ref _lastVideoFrameTicks, 0);
        owner.Dispose();
        mediaOwner?.Dispose();
        SetState(ReachSessionState.Lost);
        RaiseStatus("Reach connection lost. Use reconnect to resume.");
        ConnectionLost?.Invoke();
    }

    private void SetState(ReachSessionState state)
    {
        if (Interlocked.Exchange(ref _state, (int)state) == (int)state)
            return;

        StateChanged?.Invoke(state);
    }

    private void RaiseStatus(string status)
    {
        StatusChanged?.Invoke(status);
    }

    private static IPEndPoint ParseEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(
                endpoint.Contains("://", StringComparison.Ordinal)
                    ? endpoint
                    : $"tcp://{endpoint}",
                UriKind.Absolute,
                out var uri)
            || uri.Port <= 0)
        {
            throw new FormatException($"Invalid Reach endpoint: {endpoint}");
        }

        var address = IPAddress.TryParse(uri.Host, out var parsed)
            ? parsed
            : Dns.GetHostAddresses(uri.Host)
                .First(static item => item.AddressFamily == AddressFamily.InterNetwork);
        return new IPEndPoint(address, uri.Port);
    }

    private static ReachCapabilities GetCapabilities(ReachPlatform platform) =>
        platform switch
        {
            ReachPlatform.Windows => ReachCapabilities.WindowsClient,
            ReachPlatform.Linux => ReachCapabilities.LinuxClient,
            ReachPlatform.Android => ReachCapabilities.AndroidClient,
            _ => new ReachCapabilities(ReachCapability.None),
        };

    private static async Task<byte[]> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }

        return hash.GetHashAndReset();
    }
}
