using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Novolis.Reach.Protocol;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Client;

/// <summary>Owns one client-side Reach control connection.</summary>
public sealed class ReachClientSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private long _sequence;
    private string? _lastEndpoint;
    private ReachPlatform _platform;
    private string? _clientName;
    private readonly Guid _sessionId = Guid.NewGuid();
    private long _lastVideoSequence;

    /// <summary>Raised when the session status changes.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Raised for each encoded video frame received from the host.</summary>
    public event Action<ReachVideoFrame>? VideoFrameReceived;

    /// <summary>Raised for each remote audio block received from the host.</summary>
    public event Action<ReachAudioFrame>? AudioFrameReceived;

    /// <summary>Gets negotiated capabilities after connection.</summary>
    public ReachCapabilities? NegotiatedCapabilities { get; private set; }

    /// <summary>Gets whether the control channel is connected.</summary>
    public bool IsConnected => _stream is not null;

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
        _lastEndpoint = endpoint;
        _platform = platform;
        _clientName = clientName;
        var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address.Address, address.Port, cancellationToken)
            .ConfigureAwait(false);
        var stream = client.GetStream();
        _client = client;
        _stream = stream;
        RaiseStatus($"Connected to {address.Address}:{address.Port}");

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

            var hostHello = await ReadAsync<ReachHostHello>(cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(hostHello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hostHello.ProtocolVersion))
            {
                throw new InvalidDataException("The remote host does not speak Reach protocol 1.x.");
            }

            var hostCapabilities = await ReadAsync<ReachCapabilitiesMessage>(cancellationToken)
                .ConfigureAwait(false);
            NegotiatedCapabilities = ReachCapabilities.Intersect(
                hostCapabilities.Capabilities,
                GetCapabilities(platform));
            if (_lastVideoSequence == 0)
            {
                await SendAsync(
                        ReachMessageType.SessionOpen,
                        new ReachSessionOpen(_sessionId, string.Empty),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendAsync(
                        ReachMessageType.SessionResume,
                        new ReachSessionResume(_sessionId, _lastVideoSequence),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            _receiveCancellation = new CancellationTokenSource();
            _receiveTask = Task.Run(
                () => ReceiveLoopAsync(_receiveCancellation.Token),
                CancellationToken.None);
            RaiseStatus(
                $"Connected to {hostHello.HostName}; "
                + $"video={string.Join(",", NegotiatedCapabilities.OfferedVideoCodecs)}");
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
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
            catch (OperationCanceledException) when (receiveCancellation?.IsCancellationRequested == true)
            {
            }
        }
        receiveCancellation?.Dispose();
        NegotiatedCapabilities = null;
        RaiseStatus("Disconnected");
    }

    /// <summary>Reconnects to the last endpoint and requests session resume.</summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_lastEndpoint is null || _clientName is null)
            throw new InvalidOperationException("No previous Reach endpoint is available.");

        await DisconnectAsync().ConfigureAwait(false);
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

    private async Task<T> ReadAsync<T>(CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("Reach is not connected.");
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach host closed the control channel.");
        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
        return ReachMessageCodec.ReadBody<T>(envelope);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
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

                var envelope = ReachMessageCodec.Deserialize(frame.Payload);
                if (envelope.Type == ReachMessageType.VideoFrame)
                {
                    var video = ReachMessageCodec.ReadBody<ReachVideoFrame>(envelope);
                    Interlocked.Exchange(ref _lastVideoSequence, video.Sequence);
                    VideoFrameReceived?.Invoke(video);
                }
                else if (envelope.Type == ReachMessageType.AudioFrame)
                    AudioFrameReceived?.Invoke(
                        ReachMessageCodec.ReadBody<ReachAudioFrame>(envelope));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RaiseStatus($"Receive failed: {exception.Message}");
        }
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

    private void RaiseStatus(string status) => StatusChanged?.Invoke(status);

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
