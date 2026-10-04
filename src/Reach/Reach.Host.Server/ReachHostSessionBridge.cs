using System.Net.Sockets;
using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostSessionBridge(ReachHostRuntime runtime)
{
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ILocalIpcConnection? connection = null;
            try
            {
                connection = await TryConnectAsync(
                        cancellationToken,
                        TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                if (connection is null)
                {
                    runtime.Broadcast.TryStartHelper();
                    connection = await TryConnectAsync(
                            cancellationToken,
                            TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false);
                }

                if (connection is null)
                {
                    runtime.WriteLog("Interactive session helper is not ready yet.");
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                Interlocked.Exchange(ref runtime.SessionConnection, connection);
                runtime.WriteLog("Interactive session helper connected.");
                var pendingClient = runtime.Clients.Values
                    .Where(static client => client.IsReady)
                    .OrderBy(static client => client.Id)
                    .FirstOrDefault();
                if (pendingClient is not null)
                {
                    await SendCommandAsync(
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
                    if (!Enum.TryParse<ReachMessageType>(
                            frame.Name,
                            ignoreCase: true,
                            out var messageType)
                        || !ReachHostFraming.IsHostToClientFrame(frame.Kind, messageType))
                    {
                        continue;
                    }

                    if (runtime.SharingPaused && frame.Kind == "media")
                        continue;

                    if (messageType == ReachMessageType.VideoFrame)
                    {
                        var video = ReachMessageCodec.ReadBody<ReachVideoFrame>(
                            ReachMessageCodec.Deserialize(frame.Payload));
                        runtime.Performance.RecordReceived(
                            video.AccessUnit.Length,
                            video.Timestamp);
                    }

                    await runtime.Broadcast.SendPayloadAsync(
                            messageType,
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
                runtime.WriteLog(
                    $"Interactive session helper unavailable: {exception.Message}");
            }
            finally
            {
                var activeConnection = Interlocked.CompareExchange(
                    ref runtime.SessionConnection,
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

    internal async Task<bool> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (Volatile.Read(ref runtime.SessionConnection) is not null)
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)
                .ConfigureAwait(false);
        }

        return Volatile.Read(ref runtime.SessionConnection) is not null;
    }

    internal async Task ForwardAsync(
        ReachMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var connection = Volatile.Read(ref runtime.SessionConnection)
            ?? throw new InvalidOperationException(
                "The interactive Reach host is not connected.");

        await runtime.SessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.SendAsync(
                new LocalIpcFrame(
                    Interlocked.Increment(ref runtime.LocalSequence),
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
            runtime.SessionGate.Release();
        }
    }

    internal async Task SendCommandAsync<T>(
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var connection = runtime.SessionConnection;
        if (connection is null)
            return;

        await runtime.SessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.SendAsync(
                new LocalIpcFrame(
                    Interlocked.Increment(ref runtime.LocalSequence),
                    "control",
                    type.ToString(),
                    ReachMessageCodec.Serialize(
                        type,
                        Interlocked.Increment(ref runtime.LocalSequence),
                        message)),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            runtime.SessionGate.Release();
        }
    }

    internal async Task NotifySessionEndedAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        var sends = runtime.Clients.Values
            .Where(static client => client.IsReady)
            .Select(client => NotifySessionEndedAsync(
                client,
                reason,
                cancellationToken));
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    internal async Task NotifySessionEndedAsync(
        ReachHostClientConnection connection,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendSessionEndedAsync(connection, reason, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
                or ObjectDisposedException
                or SocketException
                or InvalidOperationException)
        {
            runtime.WriteLog(
                $"Could not notify client {connection.Id} that the session ended: {exception.Message}");
        }
    }

    internal async Task SendSessionEndedAsync(
        ReachHostClientConnection connection,
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

    private static async Task<ILocalIpcConnection?> TryConnectAsync(
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
                    new LocalIpcEndpoint(ReachHostRuntime.SessionEndpoint),
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
}
