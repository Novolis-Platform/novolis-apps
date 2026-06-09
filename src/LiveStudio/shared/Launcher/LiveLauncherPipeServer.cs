using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace LiveStudio.Shared.Launcher;

public sealed class LiveLauncherPipeServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, StreamWriter> _clients = new();
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _acceptTask;
    private LiveLauncherStatus _status = LiveLauncherStatus.Starting("Launcher pipe server starting.");

    public void Start()
    {
        _acceptTask ??= AcceptLoopAsync(_shutdown.Token);
    }

    public void UpdateStatus(LiveLauncherStatus status)
    {
        _status = status;
        Broadcast(status);
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var client in _clients.Values)
            client.Dispose();

        _clients.Clear();
        _shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                LiveLauncherEndpoints.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                break;
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        _clients[clientId] = writer;

        try
        {
            await writer.WriteLineAsync(LiveLauncherProtocol.Serialize(_status)).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                    break;

                if (string.Equals(line, "status", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync(LiveLauncherProtocol.Serialize(_status)).ConfigureAwait(false);
                else
                    await writer.WriteLineAsync("pong").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            reader.Dispose();
            writer.Dispose();
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Broadcast(LiveLauncherStatus status)
    {
        var payload = LiveLauncherProtocol.Serialize(status);
        foreach (var client in _clients.Values)
        {
            try
            {
                client.WriteLine(payload);
            }
            catch (IOException)
            {
            }
        }
    }
}
