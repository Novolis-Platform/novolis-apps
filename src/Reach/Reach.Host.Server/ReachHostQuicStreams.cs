using Novolis.Transports;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostQuicStreams(ReachHostRuntime runtime)
{
    internal async Task HandleInboundAsync(
        ReachHostClientConnection connection,
        ITransportStream transportStream,
        CancellationToken cancellationToken)
    {
        var attached = false;
        try
        {
            var envelope = await ReachHostFraming.ReadEnvelopeAsync(
                    transportStream.Stream,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EndOfStreamException(
                    "Reach QUIC stream closed during hello.");
            switch (envelope.Type)
            {
                case ReachMessageType.MediaHello:
                {
                    var hello = ReachMessageCodec.ReadBody<ReachMediaHello>(envelope);
                    ValidateHello(
                        hello.AppId,
                        hello.ProtocolVersion,
                        hello.SessionId,
                        connection.SessionId,
                        "media");
                    connection.AttachMediaStream(connection.Transport, transportStream);
                    attached = true;
                    runtime.WriteLog(
                        $"QUIC media stream attached to client {connection.Id}.");
                    await transportStream.Stream.CopyToAsync(
                            Stream.Null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                case ReachMessageType.BulkHello:
                {
                    var hello = ReachMessageCodec.ReadBody<ReachBulkHello>(envelope);
                    ValidateHello(
                        hello.AppId,
                        hello.ProtocolVersion,
                        hello.SessionId,
                        connection.SessionId,
                        "bulk");
                    runtime.WriteLog(
                        $"QUIC bulk stream attached to client {connection.Id}.");
                    await HandleBulkAsync(
                            connection,
                            transportStream.Stream,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                default:
                    throw new InvalidDataException(
                        "Reach QUIC stream did not send MediaHello or BulkHello.");
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog($"Reach QUIC stream ended: {exception.Message}");
        }
        finally
        {
            if (attached)
                connection.DetachMediaStream(transportStream.Stream);
            else
                await transportStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleBulkAsync(
        ReachHostClientConnection connection,
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envelope = await ReachHostFraming.ReadEnvelopeAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
            if (envelope is null)
                return;

            switch (envelope.Type)
            {
                case ReachMessageType.FileOffer:
                    await runtime.Files.HandleOfferAsync(
                            connection,
                            ReachMessageCodec.ReadBody<ReachFileOffer>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ReachMessageType.FileChunk:
                    await runtime.Files.HandleChunkAsync(
                            ReachMessageCodec.ReadBody<ReachFileChunk>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ReachMessageType.FileComplete:
                    await runtime.Files.HandleCompleteAsync(
                            connection,
                            ReachMessageCodec.ReadBody<ReachFileComplete>(envelope),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidDataException(
                        "Reach QUIC bulk stream carried an unsupported message.");
            }
        }
    }

    private static void ValidateHello(
        string appId,
        string protocolVersion,
        Guid sessionId,
        Guid expectedSessionId,
        string streamName)
    {
        if (!string.Equals(appId, ReachProtocol.AppId, StringComparison.Ordinal)
            || !ReachProtocol.IsCompatible(protocolVersion)
            || sessionId != expectedSessionId)
        {
            throw new InvalidDataException(
                $"Incompatible Reach QUIC {streamName} hello.");
        }
    }
}
