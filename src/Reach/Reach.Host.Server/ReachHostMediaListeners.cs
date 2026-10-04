using System.Net;
using System.Net.Sockets;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.Tcp;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostMediaListeners(ReachHostRuntime runtime)
{
    internal async Task RunTcpAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        TcpTransportListener listener;
        try
        {
            listener = new TcpTransportListener(
                new IPEndPoint(address, ReachProtocol.MediaPort));
        }
        catch (SocketException exception)
        {
            runtime.WriteLog(
                $"Could not listen for Reach media on {address}:{ReachProtocol.MediaPort}: {exception.Message}");
            return;
        }

        runtime.MediaListeners.Add(listener);
        runtime.WriteLog(
            $"Listening for Reach media on {address}:{ReachProtocol.MediaPort}.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var transport = await listener.AcceptConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
                _ = HandleTcpClientAsync(transport, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException exception)
        {
            runtime.WriteLog(
                $"Reach media listener on {address} stopped: {exception.Message}");
        }
        finally
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal async Task RunDatagramAsync(
        ITransportDatagramChannel channel,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var datagram = await channel.ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!ReachDatagramPacketCodec.TryDecodeHandshake(
                        datagram.Payload,
                        out var sessionId,
                        out var token))
                {
                    continue;
                }

                var connection = runtime.Clients.Values.FirstOrDefault(candidate =>
                    candidate.IsReady
                    && candidate.DatagramSession?.SessionId == sessionId
                    && string.Equals(
                        candidate.DatagramSession.Token,
                        token,
                        StringComparison.Ordinal)
                    && ReachHostFraming.IsSameRemoteAddress(
                        candidate.RemoteEndPoint,
                        datagram.RemoteEndpoint));
                if (connection is null)
                    continue;

                connection.AttachDatagram(channel, datagram.RemoteEndpoint);
                runtime.WriteLog(
                    $"Authenticated UDP media for client {connection.Id} from "
                    + $"{datagram.RemoteEndpoint}.");
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog($"Reach UDP media listener stopped: {exception.Message}");
        }
    }

    internal async Task AcceptQuicStreamsAsync(
        ReachHostClientConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var transportStream = await connection.Transport
                        .AcceptInboundStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                _ = runtime.Quic.HandleInboundAsync(
                    connection,
                    transportStream,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog($"Reach QUIC stream accept loop ended: {exception.Message}");
        }
    }

    private async Task HandleTcpClientAsync(
        ITransportConnection transport,
        CancellationToken cancellationToken)
    {
        ITransportStream? transportStream = null;
        ReachHostClientConnection? connection = null;
        var attached = false;
        try
        {
            transportStream = await transport.AcceptInboundStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var envelope = await ReachHostFraming.ReadEnvelopeAsync(
                    transportStream.Stream,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EndOfStreamException(
                    "Reach media client closed during hello.");
            if (envelope.Type != ReachMessageType.MediaHello)
                throw new InvalidDataException(
                    "Reach media connection did not send MediaHello.");

            var hello = ReachMessageCodec.ReadBody<ReachMediaHello>(envelope);
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach media hello.");
            }

            var remoteAddress =
                (transport.Info.RemoteEndPoint as IPEndPoint)?.Address;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                connection = runtime.Clients.Values.FirstOrDefault(candidate =>
                    candidate.IsReady
                    && candidate.SessionId == hello.SessionId
                    && (remoteAddress is null
                        || (candidate.RemoteEndPoint as IPEndPoint)?.Address
                            .Equals(remoteAddress) == true));
                if (connection is not null)
                    break;

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (connection is null)
                throw new InvalidDataException(
                    "Reach media hello has no active control session.");

            connection.AttachMediaStream(transport, transportStream);
            attached = true;
            runtime.WriteLog($"Media channel attached to client {connection.Id}.");
            await transportStream.Stream.CopyToAsync(Stream.Null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog($"Reach media connection ended: {exception.Message}");
        }
        finally
        {
            if (connection is not null && transportStream is not null && attached)
                connection.DetachMediaStream(transportStream.Stream);
            else
            {
                if (transportStream is not null)
                    await transportStream.DisposeAsync().ConfigureAwait(false);
                await transport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
