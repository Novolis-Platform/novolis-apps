using System.Security.Cryptography;
using Novolis.Reach.Transport;
using Novolis.Transports;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostRemoteClient(ReachHostRuntime runtime)
{
    internal async Task HandleAsync(
        ReachHostClientConnection connection,
        CancellationToken cancellationToken)
    {
        runtime.WriteLog(
            $"Client {connection.Id} connected from {connection.RemoteEndPoint}.");
        try
        {
            var hello = await ReachHostFraming.ReadMessageAsync<ReachClientHello>(
                    ReachMessageType.ClientHello,
                    connection.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
            runtime.WriteLog($"Client {connection.Id} sent {hello.Platform} hello.");
            if (!string.Equals(hello.AppId, ReachProtocol.AppId, StringComparison.Ordinal)
                || !ReachProtocol.IsCompatible(hello.ProtocolVersion))
            {
                throw new InvalidDataException("Incompatible Reach client hello.");
            }

            if (Volatile.Read(ref runtime.HostingStopped) != 0)
                throw new InvalidOperationException(
                    "Reach hosting has been stopped by the operator.");

            var clientCapabilities =
                await ReachHostFraming.ReadMessageAsync<ReachCapabilitiesMessage>(
                        ReachMessageType.ClientCapabilities,
                        connection.Stream,
                        cancellationToken)
                    .ConfigureAwait(false);
            runtime.WriteLog($"Client {connection.Id} capabilities received.");
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
                        runtime.Endpoints),
                    cancellationToken)
                .ConfigureAwait(false);
            await connection.SendAsync(
                    ReachMessageType.HostCapabilities,
                    new ReachCapabilitiesMessage(negotiated),
                    cancellationToken)
                .ConfigureAwait(false);
            connection.IsReady = true;
            runtime.WriteLog($"Client {connection.Id} handshake complete.");
            if (connection.Transport.Info.Kind == TransportKind.Quic)
            {
                _ = runtime.Media.AcceptQuicStreamsAsync(connection, cancellationToken);
            }

            await ReadControlLoopAsync(connection, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog($"Client {connection.Id} ended: {exception.Message}");
        }
        finally
        {
            await ReachHostRemoteCleanup.CleanupAsync(
                    runtime,
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal async Task OfferDatagramAsync(
        ReachHostClientConnection connection,
        CancellationToken cancellationToken)
    {
        var channel = Volatile.Read(ref runtime.DatagramChannel);
        if (connection.Transport.Info.Kind != TransportKind.Quic
            || channel is null
            || channel.LocalEndPoint is not System.Net.IPEndPoint localEndpoint)
        {
            connection.ClearDatagram();
            return;
        }

        var session = new ReachDatagramSession(
            connection.SessionId,
            RandomNumberGenerator.GetBytes(32),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));
        connection.SetDatagramSession(session);
        await connection.SendAsync(
                ReachMessageType.DatagramOffer,
                new ReachDatagramOffer(
                    session.SessionId,
                    localEndpoint.Port,
                    session.Token,
                    Convert.ToBase64String(session.Key)),
                cancellationToken)
            .ConfigureAwait(false);
        runtime.WriteLog($"Offered authenticated UDP media to client {connection.Id}.");
    }

    private async Task ReadControlLoopAsync(
        ReachHostClientConnection connection,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envelope = await ReachHostFraming.ReadEnvelopeAsync(
                    connection.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
            if (envelope is null)
                return;

            if (!await DispatchAsync(connection, envelope, cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task<bool> DispatchAsync(
        ReachHostClientConnection connection,
        ReachMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.Type)
        {
            case ReachMessageType.LatencyProbe:
            {
                var probe = ReachMessageCodec.ReadBody<ReachLatencyProbe>(envelope);
                await connection.SendAsync(
                        ReachMessageType.LatencyResponse,
                        new ReachLatencyResponse(probe.Id, probe.SentUtcTicks),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            case ReachMessageType.SessionClose:
            {
                var close = ReachMessageCodec.ReadBody<ReachSessionClose>(envelope);
                connection.SessionId = close.SessionId;
                if (!runtime.Clients.Values.Any(client =>
                        client.IsReady && client.Id != connection.Id))
                {
                    connection.SessionCloseForwarded = true;
                    await runtime.Session.SendCommandAsync(
                            ReachMessageType.SessionClose,
                            close,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return false;
            }
            case ReachMessageType.SessionOpen:
            case ReachMessageType.SessionResume:
                return await OpenOrResumeAsync(
                        connection,
                        envelope,
                        cancellationToken)
                    .ConfigureAwait(false);
            case ReachMessageType.FileOffer:
                await runtime.Files.HandleOfferAsync(
                        connection,
                        ReachMessageCodec.ReadBody<ReachFileOffer>(envelope),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case ReachMessageType.FileChunk:
                await runtime.Files.HandleChunkAsync(
                        ReachMessageCodec.ReadBody<ReachFileChunk>(envelope),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case ReachMessageType.FileComplete:
                await runtime.Files.HandleCompleteAsync(
                        connection,
                        ReachMessageCodec.ReadBody<ReachFileComplete>(envelope),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case ReachMessageType.PointerMove:
            case ReachMessageType.PointerButton:
            case ReachMessageType.PointerWheel:
            case ReachMessageType.KeyDown:
            case ReachMessageType.KeyUp:
            case ReachMessageType.TextInput:
            case ReachMessageType.ClipboardChanged:
            case ReachMessageType.ClipboardContent:
            case ReachMessageType.DisplaySelect:
            case ReachMessageType.DisplayResize:
            case ReachMessageType.VideoStreamConfiguration:
            case ReachMessageType.RequestKeyFrame:
            case ReachMessageType.AudioStreamConfiguration:
                await runtime.Session.ForwardAsync(envelope, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return true;
        }
    }

    private async Task<bool> OpenOrResumeAsync(
        ReachHostClientConnection connection,
        ReachMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!await runtime.Session.WaitForConnectionAsync(cancellationToken)
                .ConfigureAwait(false))
        {
            await runtime.Session.SendSessionEndedAsync(
                    connection,
                    "The interactive Reach host is unavailable.",
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (envelope.Type == ReachMessageType.SessionOpen)
        {
            var open = ReachMessageCodec.ReadBody<ReachSessionOpen>(envelope);
            connection.SessionId = open.SessionId;
            connection.RequestedDisplayId = open.RequestedDisplayId;
            connection.EnableAudio = open.EnableAudio
                && connection.Capabilities?.Supports(ReachCapability.Audio) == true;
            await OfferDatagramAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            await runtime.Session.SendCommandAsync(
                    ReachMessageType.SessionOpen,
                    open with { EnableAudio = connection.EnableAudio },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var resume = ReachMessageCodec.ReadBody<ReachSessionResume>(envelope);
            connection.SessionId = resume.SessionId;
            connection.LastVideoSequence = resume.LastVideoSequence;
            connection.EnableAudio = resume.EnableAudio
                && connection.Capabilities?.Supports(ReachCapability.Audio) == true;
            await OfferDatagramAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            await runtime.Session.SendCommandAsync(
                    ReachMessageType.SessionResume,
                    resume with { EnableAudio = connection.EnableAudio },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await runtime.Operator.SendSharingStateAsync(connection, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
