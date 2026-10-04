namespace Novolis.Reach.Host.Server;

internal static class ReachHostRemoteCleanup
{
    internal static async Task CleanupAsync(
        ReachHostRuntime runtime,
        ReachHostClientConnection connection,
        CancellationToken cancellationToken)
    {
        runtime.Clients.TryRemove(connection.Id, out _);
        if (connection.IsReady
            && !connection.SessionCloseForwarded
            && !runtime.Clients.Values.Any(static client => client.IsReady))
        {
            try
            {
                await runtime.Session.SendCommandAsync(
                        ReachMessageType.SessionClose,
                        new ReachSessionClose(
                            connection.SessionId,
                            "Client connection ended."),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
            }
        }

        foreach (var transfer in runtime.FileTransfers.Values.Where(
                     transfer => transfer.ClientId == connection.Id))
        {
            if (runtime.FileTransfers.TryRemove(transfer.TransferId, out var removed))
                await removed.DisposeAsync().ConfigureAwait(false);
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        runtime.WriteLog($"Client {connection.Id} disconnected.");
    }
}
