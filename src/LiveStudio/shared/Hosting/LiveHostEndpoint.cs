using System.IO.Pipes;

namespace LiveStudio.Shared.Hosting;

/// <summary>
/// Waits until the Live host IPC endpoint is actually accepting connections.
/// Process.Start alone is not enough — <c>dotnet run</c> can take many seconds before the pipe exists.
/// </summary>
public static class LiveHostEndpoint
{
    public const string NamedPipeName = "novolis-audio-live";

    public static string UnixSocketPath { get; } =
        Path.Combine(Path.GetTempPath(), "novolis-audio-live.sock");

    public static async Task WaitUntilListeningAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        Exception? lastError = null;
        while (!linked.IsCancellationRequested)
        {
            try
            {
                if (await TryConnectOnceAsync(linked.Token).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            try
            {
                await Task.Delay(200, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var detail = lastError is null ? string.Empty : $" Last error: {lastError.Message}";
        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds:0}s waiting for the live host IPC endpoint ({Describe()}).{detail}");
    }

    public static async Task<bool> IsListeningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
            return await TryConnectOnceAsync(timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static string Describe() =>
        OperatingSystem.IsWindows()
            ? $"NamedPipe '{NamedPipeName}'"
            : $"Unix socket '{UnixSocketPath}'";

    private static async Task<bool> TryConnectOnceAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            await using var client = new NamedPipeClientStream(
                ".",
                NamedPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (!File.Exists(UnixSocketPath))
            return false;

        // Presence is enough for readiness; LocalIpc client will open the real session.
        return true;
    }
}
