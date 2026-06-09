using System.IO.Pipes;
using System.Text;

namespace LiveStudio.Shared.Launcher;

public sealed class LiveLauncherClient : IAsyncDisposable
{
    private readonly object _gate = new();
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;

    public LiveLauncherStatus? CurrentStatus { get; private set; }

    public event Action<LiveLauncherStatus>? StatusChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_reader is not null)
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LiveLauncherEndpoints.ClientConnectTimeout);

        var pipe = new NamedPipeClientStream(
            ".",
            LiveLauncherEndpoints.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

        _reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        _listenCts = new CancellationTokenSource();
        _listenTask = ListenAsync(_listenCts.Token);
        await RequestStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LiveLauncherStatus> WaitForHostReadyAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LiveLauncherEndpoints.HostReadyTimeout);

        while (!timeout.IsCancellationRequested)
        {
            var status = CurrentStatus;
            if (status is { IsFatal: true })
                throw new InvalidOperationException(status.Message);

            if (status is { IsHostReady: true })
                return status;

            await RequestStatusAsync(timeout.Token).ConfigureAwait(false);
            await Task.Delay(250, timeout.Token).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "Timed out waiting for the live host. Start the launcher first: dotnet run --project src/LiveStudio/launcher/LiveStudio.Launcher.csproj");
    }

    public async Task RequestStatusAsync(CancellationToken cancellationToken = default)
    {
        var writer = _writer ?? throw new InvalidOperationException("Connect to the launcher before requesting status.");
        await writer.WriteLineAsync("status").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_listenCts is not null)
        {
            await _listenCts.CancelAsync().ConfigureAwait(false);
        }

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _listenCts?.Dispose();
        _reader?.Dispose();
        _writer?.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var reader = _reader ?? throw new InvalidOperationException("Launcher client is not connected.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                    break;

                if (string.Equals(line, "pong", StringComparison.Ordinal))
                    continue;

                var status = LiveLauncherProtocol.Deserialize(line);
                lock (_gate)
                {
                    CurrentStatus = status;
                }

                StatusChanged?.Invoke(status);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }
}
