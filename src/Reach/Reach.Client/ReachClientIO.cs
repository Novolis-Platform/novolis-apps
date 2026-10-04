using Novolis.Transports.Framing;

namespace Novolis.Reach.Client;

internal static class ReachClientIO
{
    internal static async Task SendAsync<T>(
        ReachClientSession session,
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var stream = session._stream
            ?? throw new InvalidOperationException("Reach is not connected.");
        await SendToStreamAsync(
                session,
                stream,
                session._sendGate,
                type,
                message,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task SendToStreamAsync<T>(
        ReachClientSession session,
        Stream stream,
        SemaphoreSlim gate,
        ReachMessageType type,
        T message,
        CancellationToken cancellationToken)
    {
        var payload = ReachMessageCodec.Serialize(
            type,
            Interlocked.Increment(ref session._sequence),
            message);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LengthPrefixedFrameCodec.WriteAsync(stream, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    internal static async Task<T> ReadAsync<T>(
        ReachClientSession session,
        ReachMessageType expectedType,
        CancellationToken cancellationToken)
    {
        var stream = session._stream
            ?? throw new InvalidOperationException("Reach is not connected.");
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach host closed the control channel.");
        var envelope = ReachMessageCodec.Deserialize(frame.Payload);
        return ReachMessageCodec.ReadBody<T>(envelope, expectedType);
    }

    internal static ReachCapabilities GetCapabilities(ReachPlatform platform) =>
        platform switch
        {
            ReachPlatform.Windows => ReachCapabilities.WindowsClient,
            ReachPlatform.Linux => ReachCapabilities.LinuxClient,
            ReachPlatform.Android => ReachCapabilities.AndroidClient,
            _ => new ReachCapabilities(ReachCapability.None),
        };
}
