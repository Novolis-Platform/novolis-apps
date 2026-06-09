using LiveStudio.Shared.Launcher;

namespace LiveStudio.Shared.Hosting;

public sealed class LiveHostWatchdog : IAsyncDisposable
{
    private const int MaxRestartAttempts = 3;

    private readonly LiveHostProcess _host = new();
    private readonly object _statusGate = new();
    private int _restartCount;
    private LiveLauncherStatus _status = LiveLauncherStatus.Starting("Waiting to start live host.");

    public event Action<LiveLauncherStatus>? StatusChanged;

    public LiveLauncherStatus CurrentStatus
    {
        get
        {
            lock (_statusGate)
            {
                return _status;
            }
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Publish(LiveLauncherStatus.Starting("Starting live host..."));
        await _host.StartAsync(cancellationToken).ConfigureAwait(false);
        Publish(LiveLauncherStatus.Running("Live host is running.", _restartCount));

        while (!cancellationToken.IsCancellationRequested)
        {
            var exitCode = await _host.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                break;

            var recovered = false;
            for (var attempt = 1; attempt <= MaxRestartAttempts; attempt++)
            {
                Publish(LiveLauncherStatus.Restarting(
                    $"Live host exited with code {exitCode}. Restart attempt {attempt}/{MaxRestartAttempts}...",
                    _restartCount));

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken).ConfigureAwait(false);

                try
                {
                    await _host.StartAsync(cancellationToken).ConfigureAwait(false);
                    _restartCount++;
                    recovered = true;
                    Publish(LiveLauncherStatus.Running("Live host recovered and is running again.", _restartCount));
                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRestartAttempts)
                    {
                        Publish(LiveLauncherStatus.Fatal(
                            $"Unable to restart the live host after {MaxRestartAttempts} attempts. {ex.Message}",
                            _restartCount));
                    }
                }
            }

            if (!recovered)
                return;
        }
    }

    public void PublishShutdown(string message) => Publish(LiveLauncherStatus.ShuttingDown(message));

    public async ValueTask DisposeAsync() => await _host.DisposeAsync().ConfigureAwait(false);

    private void Publish(LiveLauncherStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }

        StatusChanged?.Invoke(status);
    }
}
