namespace LiveStudio;

/// <summary>
/// Owns the live studio session lifecycle so the host starts with the UI and shuts down on exit.
/// </summary>
internal sealed class LiveStudioLauncher : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _startTask;
    private bool _disposed;

    public LiveStudioLauncher()
    {
        Session = new LiveStudioSession();
    }

    public LiveStudioSession Session { get; }

    public Task EnsureStartedAsync() =>
        _startTask ??= StartCoreAsync();

    public void RequestShutdown() => _shutdown.Cancel();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdown.Cancel();

        if (_startTask is not null)
        {
            try
            {
                await _startTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        await Session.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }

    private async Task StartCoreAsync()
    {
        try
        {
            await Session.StartAsync(_shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }
}
