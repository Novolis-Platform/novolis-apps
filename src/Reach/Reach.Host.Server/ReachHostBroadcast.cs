using System.Net.Sockets;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostBroadcast(ReachHostRuntime runtime)
{
    internal async Task SendPayloadAsync(
        ReachMessageType messageType,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var sends = runtime.Clients.Values
            .Where(client =>
                client.IsReady
                && (messageType is not ReachMessageType.AudioStreamStart
                    and not ReachMessageType.AudioFrame
                    || client.Capabilities?.Supports(ReachCapability.Audio) == true))
            .Select(client => SendToClientAsync(
                client,
                payload,
                messageType is ReachMessageType.VideoFrame
                    or ReachMessageType.AudioFrame,
                messageType is ReachMessageType.VideoFrame,
                cancellationToken));
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    internal void TryStartHelper()
    {
        if (Volatile.Read(ref runtime.HostingStopped) != 0)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now < runtime.NextSessionHelperLaunchAttempt)
            return;

        runtime.NextSessionHelperLaunchAttempt = now.AddSeconds(5);
        var executable = Path.Combine(
            AppContext.BaseDirectory,
            "Novolis.Reach.Host.Windows.exe");
        if (!File.Exists(executable))
        {
            runtime.WriteLog(
                "Reach host executable is not beside the service; waiting for an independently started host.");
            return;
        }

        if (runtime.Sessions.IsProcessRunningInActiveSession(executable))
        {
            runtime.WriteLog(
                "Interactive Reach host is already running; waiting for its IPC endpoint.");
            return;
        }

        if (runtime.Sessions.TryStartInActiveSession(
                executable,
                string.Empty,
                out var errorCode))
        {
            runtime.WriteLog("Started the interactive Reach host.");
        }
        else
        {
            runtime.WriteLog(
                $"Could not start the interactive Reach host (Win32 {errorCode}).");
        }
    }

    private async Task SendToClientAsync(
        ReachHostClientConnection connection,
        byte[] payload,
        bool media,
        bool latestFrame,
        CancellationToken cancellationToken)
    {
        try
        {
            var dropped = await connection.SendPayloadForChannelAsync(
                    payload,
                    media,
                    latestFrame,
                    cancellationToken)
                .ConfigureAwait(false);
            if (media)
                runtime.Performance.RecordSent(payload.Length);
            if (dropped)
                runtime.Performance.RecordDropped();
        }
        catch (Exception exception) when (
            exception is IOException
                or ObjectDisposedException
                or SocketException
                or InvalidOperationException)
        {
            runtime.WriteLog(
                $"Client {connection.Id} dropped during broadcast: {exception.Message}");
        }
    }
}
