using System.Net.Sockets;
using Novolis.Reach.Protocol;
using Novolis.Transports.Framing;

namespace Reach.Unit;

internal static class ReachClientSessionEmulator
{
    internal static async Task RunAsync(
        TcpListener listener,
        TaskCompletionSource<ReachPointerMove> inputReceived,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);
            using var stream = client.GetStream();
            await CompleteHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
            await SendAsync(
                    stream,
                    ReachMessageType.VideoStreamStart,
                    new ReachVideoStreamStart("H264", 2, 2, 1),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(
                    stream,
                    ReachMessageType.VideoFrame,
                    new ReachVideoFrame(
                        1,
                        2,
                        2,
                        DateTime.UtcNow.Ticks,
                        "H264",
                        true,
                        [1, 2, 3]),
                    cancellationToken)
                .ConfigureAwait(false);

            while (true)
            {
                var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                if (envelope is null)
                    return;
                if (envelope.Type == ReachMessageType.PointerMove)
                {
                    inputReceived.TrySetResult(
                        ReachMessageCodec.ReadBody<ReachPointerMove>(envelope));
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    internal static async Task RunThenCloseAsync(
        TcpListener listener,
        bool sendSessionClose,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);
            using var stream = client.GetStream();
            await CompleteHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            if (sendSessionClose)
            {
                await SendAsync(
                        stream,
                        ReachMessageType.SessionClose,
                        new ReachSessionClose(
                            Guid.Empty,
                            "Test host closed the session."),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    internal static async Task RunMediaAsync(
        TcpListener controlListener,
        TcpListener mediaListener,
        CancellationToken cancellationToken)
    {
        try
        {
            using var controlClient = await controlListener.AcceptTcpClientAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            using var controlStream = controlClient.GetStream();
            await CompleteHandshakeAsync(controlStream, cancellationToken)
                .ConfigureAwait(false);

            using (var mediaClient = await mediaListener.AcceptTcpClientAsync(
                       cancellationToken).ConfigureAwait(false))
            using (var mediaStream = mediaClient.GetStream())
            {
                var mediaHello = await ReadEnvelopeAsync(
                        mediaStream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (mediaHello?.Type is not ReachMessageType.MediaHello)
                    throw new InvalidDataException("Media hello was not received.");

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            controlListener.Stop();
            mediaListener.Stop();
        }
    }

    internal static async Task RunReconnectAsync(
        TcpListener listener,
        TaskCompletionSource<bool> reconnected,
        CancellationToken cancellationToken)
    {
        try
        {
            using (var firstClient = await listener.AcceptTcpClientAsync(
                       cancellationToken).ConfigureAwait(false))
            {
                using var firstStream = firstClient.GetStream();
                await CompleteHandshakeAsync(firstStream, cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            using var secondClient = await listener.AcceptTcpClientAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            using var secondStream = secondClient.GetStream();
            await CompleteHandshakeAsync(secondStream, cancellationToken)
                .ConfigureAwait(false);
            reconnected.TrySetResult(true);
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task CompleteHandshakeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await ReadBodyAsync<ReachClientHello>(stream, cancellationToken)
            .ConfigureAwait(false);
        var capabilities = await ReadBodyAsync<ReachCapabilitiesMessage>(
                stream,
                cancellationToken)
            .ConfigureAwait(false);
        await SendAsync(
                stream,
                ReachMessageType.HostHello,
                new ReachHostHello(
                    ReachProtocol.AppId,
                    ReachProtocol.Version,
                    "Reach.Unit Emulator",
                    ["tcp://127.0.0.1:19800"]),
                cancellationToken)
            .ConfigureAwait(false);
        await SendAsync(
                stream,
                ReachMessageType.HostCapabilities,
                new ReachCapabilitiesMessage(
                    ReachCapabilities.Intersect(
                        ReachCapabilities.WindowsHost,
                        capabilities.Capabilities)),
                cancellationToken)
            .ConfigureAwait(false);

        var open = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        if (open?.Type is not ReachMessageType.SessionOpen
            and not ReachMessageType.SessionResume)
        {
            throw new InvalidDataException("The client did not open a session.");
        }
    }

    private static async Task<T> ReadBodyAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException();
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

    private static ValueTask SendAsync<T>(
        Stream stream,
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken) =>
        LengthPrefixedFrameCodec.WriteAsync(
            stream,
            ReachMessageCodec.Serialize(type, DateTime.UtcNow.Ticks, message),
            cancellationToken);
}
