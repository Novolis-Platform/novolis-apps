using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
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
    private const string OperatorEndpoint = "Novolis.Reach.Host.Windows.Service";
    private const string SessionEndpoint = "Novolis.Reach.Host.Windows";
    private readonly ILogger<ReachHostService> _log;
    private readonly WindowsSessionManager _sessions;
    private readonly ConcurrentDictionary<long, ClientConnection> _clients = new();
    private readonly ConcurrentDictionary<Guid, FileTransferState> _fileTransfers = new();
    private readonly ConcurrentBag<TcpListener> _listeners = new();
    private readonly ConcurrentBag<TcpListener> _mediaListeners = new();
    private readonly object _messagesGate = new();
    private readonly List<string> _messages = [];
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ILocalIpcConnection? _sessionConnection;
    private long _clientSequence;
    private long _localSequence;
    private string[] _endpoints = [];
    private bool _sharingPaused;
    private DateTimeOffset _nextSessionHelperLaunchAttempt =
        DateTimeOffset.MinValue;

    /// <summary>Creates the host service.</summary>
    public ReachHostService(
        ILogger<ReachHostService> log,
        WindowsSessionManager sessions)
    {
        _log = log;
        _sessions = sessions;
    }

    /// <summary>Gets the current operator status.</summary>
    public ReachHostStatus GetStatus()
    {
        _sessions.TryGetActiveSession(out var session);
        var sessionConnection = Volatile.Read(ref _sessionConnection);
        lock (_messagesGate)
        {
            return new ReachHostStatus(
                _endpoints.Length == 0
                    ? "Waiting for LAN or Tailscale"
                    : sessionConnection is null
                        ? "Waiting for interactive session"
                        : "Running",
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

        var addresses = new[] { IPAddress.Loopback }
            .Concat(GetReachableIPv4Addresses())
            .Distinct()
            .ToArray();
        _endpoints = addresses
            .Select(static address => $"tcp://{address}:{ReachProtocol.ControlPort}")
            .ToArray();
        if (addresses.Length == 0)
            Log("No private IPv4 adapter is available; remote listening is paused.");

        var tasks = new List<Task>
        {
            RunOperatorIpcAsync(cancellationToken),
            RunSessionBridgeAsync(cancellationToken),
            RunDiscoveryAsync(cancellationToken),
        };
        foreach (var address in addresses)
        {
            tasks.Add(RunRemoteListenerAsync(address, cancellationToken));
            tasks.Add(RunMediaListenerAsync(address, cancellationToken));
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
            foreach (var listener in _mediaListeners)
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
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            Log($"Could not listen for Reach clients on {address}:{ReachProtocol.ControlPort}: {exception.Message}");
            return;
        }

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

    private async Task RunMediaListenerAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(address, ReachProtocol.MediaPort);
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            Log($"Could not listen for Reach media on {address}:{ReachProtocol.MediaPort}: {exception.Message}");
            return;
        }

        _mediaListeners.Add(listener);
        Log($"Listening for Reach media on {address}:{ReachProtocol.MediaPort}.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
                client.NoDelay = true;
                _ = HandleMediaClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException exception)
        {
            Log($"Reach media listener on {address} stopped: {exception.Message}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleMediaClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        NetworkStream? stream = null;
        ClientConnection? connection = null;
        try
        {
            stream = client.GetStream();
            var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EndOfStreamException("Reach media client closed during hello.");
            if (envelope.Type != ReachMessageType.MediaHello)
                throw new InvalidDataException("Reach media connection did not send MediaHello.");

            var hello = ReachMessageCodec.ReadBody<ReachMediaHello>(envelope);
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach media hello.");
            }

            var remoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                connection = _clients.Values.FirstOrDefault(candidate =>
                    candidate.IsReady
                    && candidate.SessionId == hello.SessionId
                    && (remoteAddress is null
                        || (candidate.RemoteEndPoint as IPEndPoint)?.Address.Equals(remoteAddress) == true));
                if (connection is not null)
                    break;

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (connection is null)
                throw new InvalidDataException("Reach media hello has no active control session.");

            connection.AttachMediaStream(stream);
            Log($"Media channel attached to client {connection.Id}.");
            await stream.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log($"Reach media connection ended: {exception.Message}");
        }
        finally
        {
            if (connection is not null && stream is not null)
                connection.DetachMediaStream(stream);
            client.Dispose();
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
                    ReachMessageType.ClientHello,
                    connection.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
            Log($"Client {connection.Id} sent {hello.Platform} hello.");
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach client hello.");
            }

            var clientCapabilities = await ReadMessageAsync<ReachCapabilitiesMessage>(
                    ReachMessageType.ClientCapabilities,
                    connection.Stream,
                    cancellationToken).ConfigureAwait(false);
            Log($"Client {connection.Id} capabilities received.");
            var negotiated = ReachCapabilities.Intersect(
                ReachCapabilities.WindowsHost,
                clientCapabilities.Capabilities);
            connection.Capabilities = negotiated;
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
            connection.IsReady = true;
            Log($"Client {connection.Id} handshake complete.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await ReadEnvelopeAsync(
                        connection.Stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (envelope is null)
                    return;

                if (envelope.Type == ReachMessageType.SessionClose)
                {
                    var close = ReachMessageCodec.ReadBody<ReachSessionClose>(envelope);
                    connection.SessionId = close.SessionId;
                    if (!_clients.Values.Any(client =>
                            client.IsReady && client.Id != connection.Id))
                    {
                        connection.SessionCloseForwarded = true;
                        await SendSessionCommandAsync(
                                ReachMessageType.SessionClose,
                                close,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    return;
                }

                if (envelope.Type == ReachMessageType.SessionOpen)
                {
                    var open = ReachMessageCodec.ReadBody<ReachSessionOpen>(envelope);
                    if (!await WaitForSessionConnectionAsync(cancellationToken)
                            .ConfigureAwait(false))
                    {
                        await SendSessionEndedAsync(
                                connection,
                                "The interactive Reach host is unavailable.",
                                cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    connection.SessionId = open.SessionId;
                    connection.RequestedDisplayId = open.RequestedDisplayId;
                    connection.EnableAudio = open.EnableAudio
                        && connection.Capabilities?.Supports(ReachCapability.Audio) == true;
                    await SendSessionCommandAsync(
                            ReachMessageType.SessionOpen,
                            open with
                            {
                                EnableAudio = connection.EnableAudio,
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (envelope.Type == ReachMessageType.SessionResume)
                {
                    var resume = ReachMessageCodec.ReadBody<ReachSessionResume>(envelope);
                    if (!await WaitForSessionConnectionAsync(cancellationToken)
                            .ConfigureAwait(false))
                    {
                        await SendSessionEndedAsync(
                                connection,
                                "The interactive Reach host is unavailable.",
                                cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    connection.SessionId = resume.SessionId;
                    connection.LastVideoSequence = resume.LastVideoSequence;
                    connection.EnableAudio = resume.EnableAudio
                        && connection.Capabilities?.Supports(ReachCapability.Audio) == true;
                    await SendSessionCommandAsync(
                            ReachMessageType.SessionResume,
                            resume with
                            {
                                EnableAudio = connection.EnableAudio,
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

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
            if (connection.IsReady
                && !connection.SessionCloseForwarded
                && !_clients.Values.Any(static client => client.IsReady))
            {
                try
                {
                    await SendSessionCommandAsync(
                            ReachMessageType.SessionClose,
                            new ReachSessionClose(
                                connection.SessionId,
                                "Client connection ended."),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }
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

    private async Task RunOperatorIpcAsync(CancellationToken cancellationToken)
    {
        await using var listener = LocalIpcTransport.CreateListener(
            new LocalIpcEndpoint(OperatorEndpoint));
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var connection = await listener.AcceptAsync(cancellationToken)
                .ConfigureAwait(false);
            await foreach (var frame in connection.ReadAllAsync(cancellationToken))
            {
                if (!string.Equals(frame.Kind, "operator", StringComparison.Ordinal))
                    continue;

                var request = ReachMessageCodec.ReadBody<ReachHostControlRequest>(
                    ReachMessageCodec.Deserialize(frame.Payload));
                var response = HandleOperatorRequest(request);
                await connection.SendAsync(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref _localSequence),
                        "operator",
                        "response",
                        ReachMessageCodec.Serialize(
                            ReachMessageType.HostStatus,
                            frame.Sequence,
                            response)),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private ReachHostControlResponse HandleOperatorRequest(ReachHostControlRequest request)
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
            ILocalIpcConnection? connection = null;
            try
            {
                connection = await TryConnectSessionHelperAsync(
                        cancellationToken,
                        TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                if (connection is null)
                {
                    TryStartSessionHelper();
                    connection = await TryConnectSessionHelperAsync(
                            cancellationToken,
                            TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false);
                }

                if (connection is null)
                {
                    Log("Interactive session helper is not ready yet.");
                    await Task.Delay(
                            TimeSpan.FromSeconds(3),
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                Interlocked.Exchange(ref _sessionConnection, connection);
                Log("Interactive session helper connected.");
                var pendingClient = _clients.Values
                    .Where(static client => client.IsReady)
                    .OrderBy(static client => client.Id)
                    .FirstOrDefault();
                if (pendingClient is not null)
                {
                    await SendSessionCommandAsync(
                            ReachMessageType.SessionOpen,
                            new ReachSessionOpen(
                                pendingClient.SessionId,
                                pendingClient.RequestedDisplayId,
                                pendingClient.EnableAudio),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await foreach (var frame in connection.ReadAllAsync(cancellationToken))
                {
                    var envelope = ReachMessageCodec.Deserialize(frame.Payload);
                    if (!IsHostToClientFrame(frame.Kind, envelope.Type))
                        continue;
                    if (_sharingPaused && frame.Kind == "media")
                        continue;

                    await BroadcastPayloadAsync(
                            envelope,
                            frame.Payload,
                            cancellationToken)
                        .ConfigureAwait(false);
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
                var activeConnection = Interlocked.CompareExchange(
                    ref _sessionConnection,
                    null,
                    connection);
                if (ReferenceEquals(activeConnection, connection)
                    && connection is not null)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }

                if (connection is not null
                    && !cancellationToken.IsCancellationRequested)
                {
                    await NotifySessionEndedAsync(
                            "The interactive Reach host connection ended.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<ILocalIpcConnection?> TryConnectSessionHelperAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        try
        {
            var client = LocalIpcTransport.CreateClient();
            return await client.ConnectAsync(
                    new LocalIpcEndpoint(SessionEndpoint),
                    timeoutCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool IsHostToClientFrame(
        string kind,
        ReachMessageType type) =>
        kind switch
        {
            "control" => type is ReachMessageType.VideoStreamStart
                or ReachMessageType.VideoStreamReset
                or ReachMessageType.DisplayTopology
                or ReachMessageType.AudioStreamStart
                or ReachMessageType.ClipboardContent,
            "media" => type is ReachMessageType.VideoFrame
                or ReachMessageType.AudioFrame,
            _ => false,
        };

    private void TryStartSessionHelper()
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _nextSessionHelperLaunchAttempt)
            return;

        _nextSessionHelperLaunchAttempt = now.AddSeconds(5);
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            "Novolis.Reach.Host.Windows.exe");
        if (!File.Exists(executable))
        {
            Log("Reach host executable is not beside the service; waiting for an independently started host.");
            return;
        }

        if (_sessions.IsProcessRunningInActiveSession(executable))
        {
            Log("Interactive Reach host is already running; waiting for its IPC endpoint.");
            return;
        }

        if (_sessions.TryStartInActiveSession(
                executable,
                string.Empty,
                out var errorCode))
        {
            Log("Started the interactive Reach host.");
        }
        else
        {
            Log($"Could not start the interactive Reach host (Win32 {errorCode}).");
        }
    }

    private async Task<bool> WaitForSessionConnectionAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (Volatile.Read(ref _sessionConnection) is not null)
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)
                .ConfigureAwait(false);
        }

        return Volatile.Read(ref _sessionConnection) is not null;
    }

    private async Task SendSessionEndedAsync(
        ClientConnection connection,
        string reason,
        CancellationToken cancellationToken)
    {
        connection.SessionCloseForwarded = true;
        await connection.SendAsync(
                ReachMessageType.SessionClose,
                new ReachSessionClose(connection.SessionId, reason),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task NotifySessionEndedAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        var sends = _clients.Values
            .Where(static client => client.IsReady)
            .Select(client => NotifySessionEndedAsync(
                client,
                reason,
                cancellationToken));
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    private async Task NotifySessionEndedAsync(
        ClientConnection connection,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendSessionEndedAsync(
                    connection,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
                or ObjectDisposedException
                or SocketException
                or InvalidOperationException)
        {
            Log($"Could not notify client {connection.Id} that the session ended: {exception.Message}");
        }
    }

    private async Task ForwardToSessionAsync(
        ReachMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var connection = Volatile.Read(ref _sessionConnection)
            ?? throw new InvalidOperationException(
                "The interactive Reach host is not connected.");

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

    private async Task BroadcastPayloadAsync(
        ReachMessageEnvelope envelope,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var sends = _clients.Values
            .Where(client =>
                client.IsReady
                && (envelope.Type is not ReachMessageType.AudioStreamStart
                    and not ReachMessageType.AudioFrame
                    || client.Capabilities?.Supports(ReachCapability.Audio) == true))
            .Select(client => BroadcastPayloadToClientAsync(
                client,
                payload,
                envelope.Type is ReachMessageType.VideoFrame
                    or ReachMessageType.AudioFrame,
                cancellationToken));
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    private async Task BroadcastPayloadToClientAsync(
        ClientConnection connection,
        byte[] payload,
        bool media,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendPayloadForChannelAsync(
                    payload,
                    media,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
                or ObjectDisposedException
                or SocketException
                or InvalidOperationException)
        {
            Log($"Client {connection.Id} dropped during broadcast: {exception.Message}");
        }
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

    private async Task RunDiscoveryAsync(CancellationToken cancellationToken)
    {
        await using var responder = new DiscoveryResponder(
            new IPEndPoint(IPAddress.Any, ReachProtocol.DiscoveryPort),
            ReachProtocol.DiscoveryProbe,
            new DiscoveryBeacon(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                Environment.MachineName,
                _endpoints));
        await responder.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<IPAddress> GetReachableIPv4Addresses()
    {
        var addresses = new List<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var address in networkInterface
                         .GetIPProperties()
                         .UnicastAddresses
                         .Select(static item => item.Address)
                         .Where(static item =>
                             item.AddressFamily == AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(item))
                         .Where(IsPrivateOrTailscaleIPv4))
            {
                if (!addresses.Contains(address))
                    addresses.Add(address);
            }
        }

        return addresses;
    }

    private static bool IsPrivateOrTailscaleIPv4(IPAddress address)
    {
        if (TailscaleAddressEnumerator.IsTailscaleIPv4(address))
            return true;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }

    private static async Task<T> ReadMessageAsync<T>(
        ReachMessageType expectedType,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach client closed the control stream.");
        return ReachMessageCodec.ReadBody<T>(envelope, expectedType);
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
        private readonly SemaphoreSlim _mediaSendGate = new(1, 1);
        private NetworkStream? _mediaStream;
        private int _disposeStarted;

        public ClientConnection(long id, TcpClient client)
        {
            Id = id;
            _client = client;
            Stream = client.GetStream();
        }

        public long Id { get; }
        public NetworkStream Stream { get; }
        public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;
        public bool IsReady { get; set; }
        public ReachCapabilities? Capabilities { get; set; }
        public Guid SessionId { get; set; }
        public string RequestedDisplayId { get; set; } = string.Empty;
        public long LastVideoSequence { get; set; }
        public bool EnableAudio { get; set; }
        public bool SessionCloseForwarded { get; set; }
        public bool HasMediaChannel => Volatile.Read(ref _mediaStream) is not null;

        public void AttachMediaStream(NetworkStream stream)
        {
            var previous = Interlocked.Exchange(ref _mediaStream, stream);
            previous?.Dispose();
        }

        public void DetachMediaStream(NetworkStream stream)
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _mediaStream, null, stream),
                    stream))
            {
                stream.Dispose();
            }
        }

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
            ThrowIfDisposed();
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
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

        public async ValueTask SendPayloadForChannelAsync(
            byte[] payload,
            bool media,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var mediaStream = Volatile.Read(ref _mediaStream);
            if (!media || mediaStream is null)
            {
                await SendPayloadAsync(payload, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _mediaSendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                await LengthPrefixedFrameCodec.WriteAsync(
                        mediaStream,
                        payload,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                DetachMediaStream(mediaStream);
                await SendPayloadAsync(payload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _mediaSendGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return;

            var mediaStream = Interlocked.Exchange(ref _mediaStream, null);
            if (mediaStream is not null)
                await mediaStream.DisposeAsync().ConfigureAwait(false);
            await Stream.DisposeAsync().ConfigureAwait(false);
            _client.Dispose();
            // The gates are intentionally left undisposed. A broadcast may
            // already be waiting on one while the client is being removed;
            // disposing it here races that waiter and tears down the service.
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                throw new ObjectDisposedException(nameof(ClientConnection));
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
