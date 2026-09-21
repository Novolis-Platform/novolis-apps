using Novolis.Reach.Protocol;
using Novolis.Transports.LocalIpc;

namespace Novolis.Reach.Host.Windows.Console;

/// <summary>Calls the headless Reach host service over a named pipe.</summary>
public sealed class ReachHostConsoleClient
{
    private const string Endpoint = "Novolis.Reach.Host.Windows.Service";

    /// <summary>Sends an operator request and waits for its response.</summary>
    public async Task<ReachHostControlResponse> SendAsync(
        ReachHostControlRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = LocalIpcTransport.CreateClient();
        await using var connection = await client.ConnectAsync(
                new LocalIpcEndpoint(Endpoint),
                cancellationToken)
            .ConfigureAwait(false);
        var sequence = DateTime.UtcNow.Ticks;
        await connection.SendAsync(
                new LocalIpcFrame(
                    sequence,
                    "console",
                    request.Command.ToString(),
                    ReachMessageCodec.Serialize(
                        ReachMessageType.HostStatus,
                        sequence,
                        request)),
                cancellationToken)
            .ConfigureAwait(false);

        await foreach (var frame in connection.ReadAllAsync(cancellationToken))
        {
            if (frame.Kind != "console")
                continue;

            var envelope = ReachMessageCodec.Deserialize(frame.Payload);
            return ReachMessageCodec.ReadBody<ReachHostControlResponse>(envelope);
        }

        throw new EndOfStreamException("Reach host service closed the operator channel.");
    }
}
