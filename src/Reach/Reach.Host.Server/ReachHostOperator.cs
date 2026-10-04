using Novolis.Transports.Framing;
using Novolis.Transports.LocalIpc;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostOperator(ReachHostRuntime runtime)
{
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var listener = LocalIpcTransport.CreateListener(
            new LocalIpcEndpoint(ReachHostRuntime.OperatorEndpoint));
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
                var response = HandleRequest(request);
                await connection.SendAsync(
                    new LocalIpcFrame(
                        Interlocked.Increment(ref runtime.LocalSequence),
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

    internal ReachHostControlResponse HandleRequest(ReachHostControlRequest request)
    {
        switch (request.Command)
        {
            case ReachHostCommand.SetSharingPaused:
                runtime.SharingPaused = request.Enabled ?? false;
                runtime.WriteLog(
                    runtime.SharingPaused
                        ? "Sharing paused by operator."
                        : "Sharing resumed by operator.");
                _ = BroadcastSharingStateAsync(runtime.SharingPaused);
                return new ReachHostControlResponse(
                    true,
                    "Sharing state changed.",
                    runtime.CreateStatus());
            case ReachHostCommand.ReconnectHelper:
                _ = ReconnectInteractiveHelperAsync();
                return new ReachHostControlResponse(
                    true,
                    "Interactive helper reconnect requested.",
                    runtime.CreateStatus());
            case ReachHostCommand.StopHosting:
                Interlocked.Exchange(ref runtime.HostingStopped, 1);
                runtime.SharingPaused = true;
                _ = StopHostingAsync();
                return new ReachHostControlResponse(
                    true,
                    "Reach hosting stopped until the service restarts.",
                    runtime.CreateStatus());
            case ReachHostCommand.GetLogs:
            case ReachHostCommand.GetStatus:
                return new ReachHostControlResponse(
                    true,
                    "Host status.",
                    runtime.CreateStatus());
            default:
                return new ReachHostControlResponse(
                    false,
                    "Unknown host command.",
                    runtime.CreateStatus());
        }
    }

    internal ValueTask SendSharingStateAsync(
        ReachHostClientConnection connection,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            ReachMessageType.SharingState,
            new ReachSharingState(
                runtime.SharingPaused,
                runtime.SharingPaused
                    ? "Sharing is paused by the host operator."
                    : "Sharing is active."),
            cancellationToken);

    private async Task ReconnectInteractiveHelperAsync()
    {
        var connection = Interlocked.Exchange(ref runtime.SessionConnection, null);
        if (connection is not null)
            await connection.DisposeAsync().ConfigureAwait(false);
        runtime.WriteLog("Interactive helper reconnect requested by operator.");
    }

    private async Task StopHostingAsync()
    {
        try
        {
            await runtime.Session.NotifySessionEndedAsync(
                    "Reach hosting was stopped by the operator.",
                    runtime.Lifetime.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runtime.Lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task BroadcastSharingStateAsync(bool paused)
    {
        try
        {
            var sends = runtime.Clients.Values
                .Where(static client => client.IsReady)
                .Select(client => client.SendAsync(
                    ReachMessageType.SharingState,
                    new ReachSharingState(
                        paused,
                        paused
                            ? "Sharing is paused by the host operator."
                            : "Sharing is active."),
                    runtime.Lifetime.Token).AsTask());
            await Task.WhenAll(sends).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runtime.Lifetime.IsCancellationRequested)
        {
        }
    }
}
