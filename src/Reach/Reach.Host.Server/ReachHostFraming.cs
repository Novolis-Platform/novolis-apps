using System.Net;
using Novolis.Transports.Framing;

namespace Novolis.Reach.Host.Server;

internal static class ReachHostFraming
{
    internal static bool IsSameRemoteAddress(EndPoint expected, EndPoint actual) =>
        expected is IPEndPoint expectedIp
        && actual is IPEndPoint actualIp
        && expectedIp.Address.Equals(actualIp.Address);

    internal static bool IsHostToClientFrame(string kind, ReachMessageType type) =>
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

    internal static async Task<T> ReadMessageAsync<T>(
        ReachMessageType expectedType,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(stream, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EndOfStreamException("Reach client closed the control stream.");
        return ReachMessageCodec.ReadBody<T>(envelope, expectedType);
    }

    internal static async Task<ReachMessageEnvelope?> ReadEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var frame = await LengthPrefixedFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return frame is null ? null : ReachMessageCodec.Deserialize(frame.Payload);
    }
}
