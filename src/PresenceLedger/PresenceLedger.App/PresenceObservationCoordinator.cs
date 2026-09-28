using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Avalonia.Mobile;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>
/// Composes platform readings into the presence engine without retaining raw
/// observations. The Android head controls when this coordinator is running.
/// </summary>
public sealed class PresenceObservationCoordinator : IAsyncDisposable
{
    static readonly TimeSpan LocationInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan WifiInterval = TimeSpan.FromSeconds(30);
    static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);
    const double LocationDistanceMeters = 40;

    readonly IServiceProvider _services;
    readonly IPresenceEngine _engine;
    readonly object _gate = new();

    CancellationTokenSource? _cancellation;
    Task? _runTask;
    Exception? _lastError;
    DateTimeOffset? _lastPositionAt;
    DateTimeOffset? _lastWifiAt;

    /// <summary>Creates an observation coordinator.</summary>
    public PresenceObservationCoordinator(
        IServiceProvider services,
        IPresenceEngine engine)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>Whether a platform observation session is currently active.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _runTask is { IsCompleted: false };
        }
    }

    /// <summary>Last location reading handed to the inference engine.</summary>
    public DateTimeOffset? LastPositionAt
    {
        get
        {
            lock (_gate)
                return _lastPositionAt;
        }
    }

    /// <summary>Last Wi-Fi reading handed to the inference engine.</summary>
    public DateTimeOffset? LastWifiAt
    {
        get
        {
            lock (_gate)
                return _lastWifiAt;
        }
    }

    /// <summary>Most recent non-fatal platform or inference error.</summary>
    public Exception? LastError
    {
        get
        {
            lock (_gate)
                return _lastError;
        }
    }

    /// <summary>
    /// Starts a session if the host exposes mobile reading sources. Desktop
    /// hosts therefore remain a history/configuration viewer.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_runTask is { IsCompleted: false })
                return Task.CompletedTask;

            var location = _services.GetService<ILocationReadingSource>();
            var wifi = _services.GetService<IWifiObservationSource>();
            if (location is null && wifi is null)
                return Task.CompletedTask;

            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lastError = null;
            _runTask = RunAsync(location, wifi, _cancellation.Token);
            return Task.CompletedTask;
        }
    }

    /// <summary>Stops the current observation session and waits for callbacks to drain.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? run;
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            run = _runTask;
            cancellation = _cancellation;
            if (run is null || cancellation is null)
                return;
            cancellation.Cancel();
        }

        try
        {
            await run.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancellation is the normal completion path for a session.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_runTask, run))
                {
                    _runTask = null;
                    _cancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (OperationCanceledException)
        {
            // Disposal is best effort.
        }
    }

    async Task RunAsync(
        ILocationReadingSource? location,
        IWifiObservationSource? wifi,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<Observation>(
            new UnboundedChannelOptions { SingleReader = true });
        var producers = new List<Task>(2);

        if (location is not null)
            producers.Add(ReadLocationsAsync(location, channel.Writer, cancellationToken));
        if (wifi is not null)
            producers.Add(ReadWifiAsync(wifi, channel.Writer, cancellationToken));

        var pump = PumpAsync(channel.Reader, cancellationToken);
        try
        {
            await Task.WhenAll(producers);
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await pump;
    }

    async Task ReadLocationsAsync(
        ILocationReadingSource source,
        ChannelWriter<Observation> writer,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var reading in source.ObserveAsync(
                    LocationInterval,
                    LocationDistanceMeters,
                    cancellationToken))
                {
                    var observation = new PositionObservation(
                        reading.At,
                        reading.Position,
                        reading.AccuracyMeters);
                    await writer.WriteAsync(observation, cancellationToken);
                    lock (_gate)
                        _lastPositionAt = reading.At;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetError(ex);
            }

            await DelayAsync(RetryInterval, cancellationToken);
        }
    }

    async Task ReadWifiAsync(
        IWifiObservationSource source,
        ChannelWriter<Observation> writer,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(WifiInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var reading = await source.ReadAsync(cancellationToken);
                // A redacted/unknown SSID is not evidence of departure. A
                // known non-matching network is useful absence evidence.
                if (reading.Status == MobileObservationStatus.Available
                    && reading.ConnectedSsid is not null)
                {
                    await writer.WriteAsync(
                        new WifiObservation(reading.At, reading.ConnectedSsid),
                        cancellationToken);
                    lock (_gate)
                        _lastWifiAt = reading.At;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetError(ex);
            }

            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    async Task PumpAsync(
        ChannelReader<Observation> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var observation in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await _engine.ProcessAsync(observation, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }
    }

    static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    void SetError(Exception exception)
    {
        lock (_gate)
            _lastError = exception;
    }
}
