using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Reach.Protocol;
using Novolis.Transports.Discovery;
using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;
using Novolis.Transports.Tailscale;
using Novolis.Windows.Sessions;

namespace Novolis.Reach.Host.Windows.Service;

/// <summary>
/// Headless Reach host service. It accepts clients on Tailscale and bridges
/// session commands to the interactive-session helper over local IPC.
/// </summary>
public sealed class ReachHostService : BackgroundService
{
    private const string ConsoleEndpoint = "Novolis.Reach.Host.Windows.Service";
    private const string SessionEndpoint = "Novolis.Reach.Host.Windows.Session";
    private readonly ILogger<ReachHostService> _log;
    private readonly TailscaleAddressEnumerator _tailscale;
    private readonly WindowsSessionManager _sessions;
    private readonly ConcurrentDictionary<long, ClientConnection> _clients = new();
    private readonly ConcurrentDictionary<Guid, FileTransferState> _fileTransfers = new();
    private readonly ConcurrentBag<TcpListener> _listeners = new();
    private readonly object _messagesGate = new();
    private readonly List<string> _messages = [];
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ILocalIpcConnection? _sessionConnection;
    private long _clientSequence;
    private long _localSequence;
    private string[] _endpoints = [];
    private bool _sharingPaused;
    private bool _sessionHelperLaunchAttempted;

    /// <summary>Creates the host service.</summary>
    public ReachHostService(
        ILogger<ReachHostService> log,
        TailscaleAddressEnumerator tailscale,
        WindowsSessionManager sessions)
    {
        _log = log;
        _tailscale = tailscale;
        _sessions = sessions;
    }

    /// <summary>Gets the current operator status.</summary>
    public ReachHostStatus GetStatus()
    {
        _sessions.TryGetActiveSession(out var session);
        lock (_messagesGate)
        {
            return new ReachHostStatus(
                _endpoints.Length == 0 ? "Waiting for Tailscale" : "Running",
                _endpoints,
                _clients.Count,
                _sharingPaused,
                session?.DomainName is { Length: > 0 } domain
                    ? $"{domain}\\{session.UserName}"
                    : session?.UserName,
                _messages.TakeLast(40).ToArray());
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            _lifetime.Token);
        var cancellationToken = linked.Token;
        Log("Reach host service starting.");

        var addresses = _tailscale.GetIPv4Addresses().ToArray();
        _endpoints = addresses
            .Select(static address => $"tcp://{address}:{ReachProtocol.ControlPort}")
            .ToArray();
        if (addresses.Length == 0)
            Log("No Tailscale IPv4 adapter is available; remote listening is paused.");

        var tasks = new List<Task>
        {
            RunConsoleIpcAsync(cancellationToken),
            RunSessionBridgeAsync(cancellationToken),
        };
        foreach (var address in addresses)
        {
            tasks.Add(RunRemoteListenerAsync(address, cancellationToken));
            tasks.Add(RunDiscoveryAsync(address, cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var listener in _listeners)
                listener.Stop();
            foreach (var client in _clients.Values)
                await client.DisposeAsync().ConfigureAwait(false);
            var sessionConnection = Interlocked.Exchange(ref _sessionConnection, null);
            if (sessionConnection is not null)
                await sessionConnection.DisposeAsync().ConfigureAwait(false);
            Log("Reach host service stopped.");
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _lifetime.Cancel();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunRemoteListenerAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(address, ReachProtocol.ControlPort);
        listener.Start();
        _listeners.Add(listener);
        Log($"Listening for Reach clients on {address}:{ReachProtocol.ControlPort}.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
                client.NoDelay = true;
                var id = Interlocked.Increment(ref _clientSequence);
                var connection = new ClientConnection(id, client);
                _clients[id] = connection;
                _ = HandleRemoteClientAsync(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException exception)
        {
            Log($"Reach listener on {address} stopped: {exception.Message}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleRemoteClientAsync(
        ClientConnection connection,
        CancellationToken cancellationToken)
    {
        Log($"Client {connection.Id} connected from {connection.RemoteEndPoint}.");
        try
        {
            var hello = await ReadMessageAsync<ReachClientHello>(
                    connection.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach client hello.");
            }

            var clientCapabilities = await ReadMessageAsync<ReachCapabilitiesMessage>(
                    connection.Stream,
                    cancellationToken).ConfigureAwait(false);
            var negotiated = ReachCapabilities.Intersect(
                ReachCapabilities.WindowsHost,
                clientCapabilities.Capabilities);
            await connection.SendAsync(
                    ReachMessageType.HostHello,
                    new ReachHostHello(
                        ReachProtocol.AppId,
                        ReachProtocol.Version,
                        Environment.MachineName,
                        _endpoints),
                    cancellationToken)
                .ConfigureAwait(false);
            await connection.SendAsync(
                    ReachMessageType.HostCapabilities,
                    new ReachCapabilitiesMessage(negotiated),
                    cancellationToken).ConfigureAwait(false);
            await SendSessionCommandAsync(
                    ReachMessageType.SessionOpen,
                    new ReachSessionOpen(Guid.NewGuid(), string.Empty),
                    cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await ReadEnvelopeAsync(
                        connection.Stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (envelope is null)
                    return;

                if (envelope.Type == ReachMessageType.SessionClose)
                    return;

                if (envelope.Type == ReachMessageType.FileOffer)
                {
                    await HandleFileOfferAsync(
                            connection,
                            ReachMessageCodec.ReadBody<ReachFileOffer>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (envelope.Type == ReachMessageType.FileChunk)
                {
                    await HandleFileChunkAsync(
                            ReachMessageCodec.ReadBody<ReachFileChunk>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (envelope.Type == ReachMessageType.FileComplete)
                {
                    await HandleFileCompleteAsync(
                            connection,
                            ReachMessageCodec.ReadBody<ReachFileComplete>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (envelope.Type is ReachMessageType.PointerMove
                    or ReachMessageType.PointerButton
                    or ReachMessageType.PointerWheel
                    or ReachMessageType.KeyDown
                    or ReachMessageType.KeyUp
                    or ReachMessageType.TextInput
                    or ReachMessageType.ClipboardChanged
                    or ReachMessageType.ClipboardContent
                    or ReachMessageType.DisplaySelect
                    or ReachMessageType.DisplayResize
                    or ReachMessageType.VideoStreamConfiguration
                    or ReachMessageType.RequestKeyFrame
                    or ReachMessageType.AudioStreamConfiguration)
                {
                    await ForwardToSessionAsync(envelope, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log($"Client {connection.Id} ended: {exception.Message}");
        }
        finally
        {
            _clients.TryRemove(connection.Id, out _);
            foreach (var transfer in _fileTransfers.Values.Where(
                         transfer => transfer.ClientId == connection.Id))
            {
                if (_fileTransfers.TryRemove(transfer.TransferId, out var removed))
                    await removed.DisposeAsync().ConfigureAwait(false);
            }
            await connection.DisposeAsync().ConfigureAwait(false);
            Log($"Client {connection.Id} disconnected.");
        }
    }

    private async Task RunConsoleIpcAsync(CancellationToken cancellationToken)
    {
        await using var listener = LocalIpcTransport.CreateListener(
            new LocalIpcEndpoint(ConsoleEndpoint));
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var connection = await listener.AcceptAsync(cancellationToken)
                .ConfigureAwait(false);
            await foreach (var frame in connection.ReadAllAsync(cancellationToken))
            {
                if (!string.Equals(frame.Kind, "console", StringComparison.Ordinal))
                    continue;

                var request = ReachMessageCodec.ReadBody<ReachHostControlRequest>(
                    ReachMessageCodec.Deserialize(frame.Payload));
                var response = HandleConsoleRequest(request);
                await connection.SendAsync(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref _localSequence),
                        "console",
                        "response",
                        ReachMessageCodec.Serialize(
                            ReachMessageType.HostStatus,
                            frame.Sequence,
                            response)),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private ReachHostControlResponse HandleConsoleRequest(ReachHostControlRequest request)
    {
        switch (request.Command)
        {
            case ReachHostCommand.SetSharingPaused:
                _sharingPaused = request.Enabled ?? false;
                Log(_sharingPaused ? "Sharing paused by operator." : "Sharing resumed by operator.");
                return new ReachHostControlResponse(true, "Sharing state changed.", GetStatus());
            case ReachHostCommand.GetLogs:
            case ReachHostCommand.GetStatus:
                return new ReachHostControlResponse(true, "Host status.", GetStatus());
            default:
                return new ReachHostControlResponse(false, "Unknown host command.", GetStatus());
        }
    }

    private async Task RunSessionBridgeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TryStartSessionHelper();
                var client = LocalIpcTransport.CreateClient();
                var connection = await client.ConnectAsync(
                        new LocalIpcEndpoint(SessionEndpoint),
                        cancellationToken)
                    .ConfigureAwait(false);
                _sessionConnection = connection;
                Log("Interactive session helper connected.");
                await connection.SendAsync(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref _localSequence),
                        "control",
                        "attach",
                        ReachMessageCodec.Serialize(
                            ReachMessageType.SessionOpen,
                            Interlocked.Increment(ref _localSequence),
                            new ReachSessionOpen(Guid.NewGuid(), string.Empty))),
                    cancellationToken).ConfigureAwait(false);

                await foreach (var frame in connection.ReadAllAsync(cancellationToken))
                {
                    if (frame.Kind == "media"
                        && (frame.Name == "VideoFrame" || frame.Name == "AudioFrame"))
                    {
                        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
                        if (!_sharingPaused)
                        {
                            if (envelope.Type == ReachMessageType.VideoFrame)
                            {
                                var packet = ReachMessageCodec.ReadBody<ReachVideoFrame>(envelope);
                                await BroadcastAsync(
                                        envelope.Type,
                                        packet,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            }
                            else if (envelope.Type == ReachMessageType.AudioFrame)
                            {
                                var packet = ReachMessageCodec.ReadBody<ReachAudioFrame>(envelope);
                                await BroadcastAsync(
                                        envelope.Type,
                                        packet,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Log($"Interactive session helper unavailable: {exception.Message}");
            }
            finally
            {
                var connection = Interlocked.Exchange(ref _sessionConnection, null);
                if (connection is not null)
                    await connection.DisposeAsync().ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void TryStartSessionHelper()
    {
        if (_sessionHelperLaunchAttempted)
            return;

        _sessionHelperLaunchAttempted = true;
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            "Novolis.Reach.Host.Windows.Session.exe");
        if (!File.Exists(executable))
        {
            Log("Session helper executable is not beside the service; waiting for an independently started helper.");
            return;
        }

        if (_sessions.TryStartInActiveSession(
                executable,
                string.Empty,
                out var errorCode))
        {
            Log("Started the interactive session helper.");
        }
        else
        {
            Log($"Could not start the interactive session helper (Win32 {errorCode}).");
        }
    }

    private async Task ForwardToSessionAsync(
        ReachMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var connection = _sessionConnection;
        if (connection is null)
            return;

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.SendAsync(
                new LocalIpcFrame(
                    Interlocked.Increment(ref _localSequence),
                    "control",
                    envelope.Type.ToString(),
                    ReachMessageCodec.Serialize(
                        envelope.Type,
                        envelope.Sequence,
                        envelope.Body)),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private async Task SendSessionCommandAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var connection = _sessionConnection;
        if (connection is null)
            return;

        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.SendAsync(
                new LocalIpcFrame(
                    Interlocked.Increment(ref _localSequence),
                    "control",
                    type.ToString(),
                    ReachMessageCodec.Serialize(
                        type,
                        Interlocked.Increment(ref _localSequence),
                        message)),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private async Task BroadcastAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var payload = ReachMessageCodec.Serialize(
            type,
            Interlocked.Increment(ref _localSequence),
            message);
        var sends = _clients.Values.Select(client =>
            client.SendPayloadAsync(payload, cancellationToken).AsTask());
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    private async Task HandleFileOfferAsync(
        ClientConnection connection,
        ReachFileOffer offer,
        CancellationToken cancellationToken)
    {
        const long maximumLength = 2L * 1024 * 1024 * 1024;
        if (offer.Length < 0 || offer.Length > maximumLength)
            throw new InvalidDataException("Reach file offer exceeds the host limit.");

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Reach",
            "Incoming");
        Directory.CreateDirectory(directory);
        var safeName = Path.GetFileName(offer.Name);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new InvalidDataException("Reach file offer has no file name.");

        var path = Path.Combine(directory, $"{offer.TransferId:N}-{safeName}");
        var transfer = new FileTransferState(
            connection.Id,
            offer.TransferId,
            path,
            offer.Length,
            offer.Hash,
            new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan));
        if (!_fileTransfers.TryAdd(offer.TransferId, transfer))
        {
            await transfer.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Reach transfer id was already active.");
        }

        Log($"Receiving {safeName} ({offer.Length} bytes) from client {connection.Id}.");
        await connection.SendAsync(
                ReachMessageType.FileOffer,
                offer,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleFileChunkAsync(
        ReachFileChunk chunk,
        CancellationToken cancellationToken)
    {
        if (!_fileTransfers.TryGetValue(chunk.TransferId, out var transfer))
            throw new InvalidDataException("Reach file chunk has no active offer.");
        if (chunk.Offset != transfer.BytesWritten)
            throw new InvalidDataException("Reach file chunk offset is not sequential.");
        if (chunk.Data.Length > 1024 * 1024
            || transfer.BytesWritten + chunk.Data.Length > transfer.ExpectedLength)
        {
            throw new InvalidDataException("Reach file chunk exceeds its offer.");
        }

        await transfer.Stream.WriteAsync(chunk.Data, cancellationToken).ConfigureAwait(false);
        transfer.BytesWritten += chunk.Data.Length;
    }

    private async Task HandleFileCompleteAsync(
        ClientConnection connection,
        ReachFileComplete complete,
        CancellationToken cancellationToken)
    {
        if (!_fileTransfers.TryRemove(complete.TransferId, out var transfer))
            throw new InvalidDataException("Reach file completion has no active offer.");

        var success = complete.Succeeded
            && transfer.BytesWritten == transfer.ExpectedLength;
        await transfer.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await transfer.DisposeAsync().ConfigureAwait(false);
        if (success)
        {
            await using var receivedFile = File.OpenRead(transfer.Path);
            var actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(receivedFile, cancellationToken)
                    .ConfigureAwait(false));
            success = string.Equals(
                actualHash,
                transfer.ExpectedHash,
                StringComparison.OrdinalIgnoreCase);
        }

        Log(success
            ? $"Received file {transfer.Path}."
            : $"Rejected incomplete or invalid file transfer {complete.TransferId}.");
        await connection.SendAsync(
                ReachMessageType.FileComplete,
                complete with
                {
                    Succeeded = success,
                    Error = success ? null : "Host validation failed.",
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunDiscoveryAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        await using var responder = new DiscoveryResponder(
            new IPEndPoint(address, ReachProtocol.DiscoveryPort),
            ReachProtocol.DiscoveryProbe,
            new DiscoveryBeacon(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                Environment.MachineName,
                [$"tcp://{address}:{ReachProtocol.ControlPort}"]));
        await responder.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadMessageAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach client closed the control stream.");
        return ReachMessageCodec.ReadBody<T>(envelope);
    }

    private static async Task<ReachMessageEnvelope?> ReadEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return frame is null ? null : ReachMessageCodec.Deserialize(frame.Payload);
    }

    private void Log(string message)
    {
        _log.LogInformation("{Message}", message);
        lock (_messagesGate)
            _messages.Add($"{DateTimeOffset.Now:HH:mm:ss} {message}");
    }

    private sealed class ClientConnection : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly SemaphoreSlim _sendGate = new(1, 1);

        public ClientConnection(long id, TcpClient client)
        {
            Id = id;
            _client = client;
            Stream = client.GetStream();
        }

        public long Id { get; }
        public NetworkStream Stream { get; }
        public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

        public async ValueTask SendAsync<T>(
            ReachMessageType type,
            T message,
            CancellationToken cancellationToken)
        {
            await SendPayloadAsync(
                ReachMessageCodec.Serialize(
                    type,
                    DateTime.UtcNow.Ticks,
                    message),
                cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask SendPayloadAsync(
            byte[] payload,
            CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await LengthPrefixedFrameCodec.WriteAsync(
                        Stream,
                        payload,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Stream.DisposeAsync().ConfigureAwait(false);
            _client.Dispose();
            _sendGate.Dispose();
        }
    }

    private sealed class FileTransferState : IAsyncDisposable
    {
        public FileTransferState(
            long clientId,
            Guid transferId,
            string path,
            long expectedLength,
            string expectedHash,
            FileStream stream)
        {
            ClientId = clientId;
            TransferId = transferId;
            Path = path;
            ExpectedLength = expectedLength;
            ExpectedHash = expectedHash;
            Stream = stream;
        }

        public long ClientId { get; }
        public Guid TransferId { get; }
        public string Path { get; }
        public long ExpectedLength { get; }
        public string ExpectedHash { get; }
        public FileStream Stream { get; }
        public long BytesWritten { get; set; }

        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }
}
