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
    static readonly TimeSpan WifiInterval = TimeSpan.FromSeconds(30);
    static readonly TimeSpan FreshFixAge = TimeSpan.FromMinutes(2);

    readonly IServiceProvider _services;
    readonly IPresenceEngine _engine;
    readonly IPresenceObservationStore _observations;
    readonly PresenceHistoryRebuild _history;
    readonly object _gate = new();

    CancellationTokenSource? _cancellation;
    Task? _runTask;
    Exception? _lastError;
    DateTimeOffset? _lastPositionAt;
    DateTimeOffset? _lastWifiAt;
    string? _lastConnectedSsid;
    string? _lastPlacement;
    bool _positionFixSkipped;

    /// <summary>Creates an observation coordinator.</summary>
    public PresenceObservationCoordinator(
        IServiceProvider services,
        IPresenceEngine engine,
        IPresenceObservationStore observations,
        PresenceHistoryRebuild history)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _observations = observations ?? throw new ArgumentNullException(nameof(observations));
        _history = history ?? throw new ArgumentNullException(nameof(history));
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

    /// <summary>Connected network from the latest Wi-Fi reading, when one was available.</summary>
    public string? LastConnectedSsid
    {
        get
        {
            lock (_gate)
                return _lastConnectedSsid;
        }
    }

    /// <summary>Place named by the latest matching network, when GPS was not required.</summary>
    public string? LastPlacement
    {
        get
        {
            lock (_gate)
                return _lastPlacement;
        }
    }

    /// <summary>Whether the latest cycle skipped GPS because a known network already placed the person.</summary>
    public bool PositionFixSkipped
    {
        get
        {
            lock (_gate)
                return _positionFixSkipped;
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
        try
        {
            await _history.RebuildAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            SetError(ex);
        }

        using var timer = new PeriodicTimer(WifiInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ObserveCycleAsync(location, wifi, cancellationToken);
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
                if (!await timer.WaitForNextTickAsync(cancellationToken))
                    return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    Task ObserveCycleAsync(
        ILocationReadingSource? location,
        IWifiObservationSource? wifi,
        CancellationToken cancellationToken) =>
        _history.RunExclusiveAsync(
            token => ObserveOnceAsync(location, wifi, token),
            cancellationToken);

    async Task ObserveOnceAsync(
        ILocationReadingSource? location,
        IWifiObservationSource? wifi,
        CancellationToken cancellationToken)
    {
        var configured = await ReadConfiguredLocationsAsync(cancellationToken);
        MobileWifiReading? reading = null;
        if (wifi is not null)
            reading = await wifi.ReadAsync(cancellationToken);

        var connectedSsid = reading is { Status: MobileObservationStatus.Available }
            ? reading.ConnectedSsid
            : null;
        var matched = WifiPlacement.Match(connectedSsid, configured);
        var held = false;
        if (matched is null
            && reading is not null
            && reading.Status != MobileObservationStatus.Available
            && HoldKnownNetwork(reading.At, configured) is { } heldPlace)
        {
            matched = heldPlace;
            connectedSsid = heldPlace.Wifi!.Ssid;
            held = true;
        }

        var skipPositionFix = matched is not null || location is null;

        if (reading is not null)
        {
            var wifiObservation = new WifiObservation(reading.At, connectedSsid);
            var record = matched is null
                ? PresenceObservationRecord.FromWifi(
                    wifiObservation,
                    MapWifiStatus(reading.Status))
                : PresenceObservationRecord.FromKnownNetwork(
                    wifiObservation,
                    matched.Area.Center,
                    matched.Area.RadiusMeters);
            await RetainAsync(record, cancellationToken);
            if (connectedSsid is not null
                && (held || reading.Status == MobileObservationStatus.Available))
            {
                await _engine.ProcessAsync(wifiObservation, cancellationToken);
                lock (_gate)
                {
                    if (!held)
                    {
                        _lastWifiAt = reading.At;
                        _lastConnectedSsid = connectedSsid;
                    }

                    _lastPlacement = matched?.DisplayName;
                    _positionFixSkipped = skipPositionFix;
                }
            }
        }

        lock (_gate)
            _positionFixSkipped = skipPositionFix;

        if (skipPositionFix || location is null)
        {
            await PublishLedgerAsync(cancellationToken);
            return;
        }

        var fix = await location.ReadFixAsync(FreshFixAge, cancellationToken);
        var alreadyRecorded = false;
        if (fix is not null)
        {
            lock (_gate)
                alreadyRecorded = _lastPositionAt == fix.At;
        }

        if (fix is not null && !alreadyRecorded)
        {
            var position = new PositionObservation(
                fix.At,
                fix.Position,
                fix.AccuracyMeters);
            await RetainAsync(
                PresenceObservationRecord.FromPosition(
                    position,
                    reading is null
                        ? RecordedWifiStatus.Unknown
                        : MapWifiStatus(reading.Status),
                    connectedSsid),
                cancellationToken);
            await _engine.ProcessAsync(position, cancellationToken);
            lock (_gate)
                _lastPositionAt = fix.At;
        }

        await PublishLedgerAsync(cancellationToken);
    }

    async Task<IReadOnlyList<TrackedLocation>> ReadConfiguredLocationsAsync(
        CancellationToken cancellationToken)
    {
        var store = _services.GetService<ITrackedLocationStore>();
        if (store is null)
            return [];

        var locations = new List<TrackedLocation>();
        await foreach (var location in store.ReadAsync(cancellationToken))
            locations.Add(location);

        return locations;
    }

    async Task PublishLedgerAsync(CancellationToken cancellationToken)
    {
        var publisher = _services.GetService<ILedgerFilePublisher>();
        if (publisher is null)
            return;

        try
        {
            await publisher.PublishAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
    }

    void SetError(Exception exception)
    {
        lock (_gate)
            _lastError = exception;
    }

    async ValueTask RetainAsync(
        PresenceObservationRecord observation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _observations.AppendAsync(observation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
    }

    TrackedLocation? HoldKnownNetwork(
        DateTimeOffset at,
        IReadOnlyList<TrackedLocation> configured)
    {
        DateTimeOffset? seenAt;
        string? seenSsid;
        lock (_gate)
        {
            seenAt = _lastWifiAt;
            seenSsid = _lastConnectedSsid;
        }

        if (seenAt is not { } seen || at < seen)
            return null;

        var place = WifiPlacement.Match(seenSsid, configured);
        if (place is null || at - seen > place.Policy.MaximumEvidenceGap)
            return null;

        return place;
    }

    static RecordedWifiStatus MapWifiStatus(MobileObservationStatus status) =>
        status switch
        {
            MobileObservationStatus.Available => RecordedWifiStatus.Available,
            MobileObservationStatus.Redacted => RecordedWifiStatus.Redacted,
            _ => RecordedWifiStatus.Unavailable,
        };
}
