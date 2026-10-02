using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>
/// Rewrites presence from retained samples using the current place definitions.
/// Adding or moving a place explains the samples already on disk.
/// </summary>
public sealed class PresenceHistoryRebuild
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly ITrackedLocationStore _locations;
    readonly IPresenceObservationStore _observations;
    readonly IPresenceEventStore _events;
    readonly IPresenceStateStore _states;

    /// <summary>Creates a rebuild over the local ledger stores.</summary>
    public PresenceHistoryRebuild(
        ITrackedLocationStore locations,
        IPresenceObservationStore observations,
        IPresenceEventStore events,
        IPresenceStateStore states)
    {
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _observations = observations ?? throw new ArgumentNullException(nameof(observations));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _states = states ?? throw new ArgumentNullException(nameof(states));
    }

    /// <summary>Replaces events and operational state from every retained sample.</summary>
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await RebuildCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs an observation cycle without overlapping a rebuild.</summary>
    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task RebuildCoreAsync(CancellationToken cancellationToken)
    {
        var locations = new List<TrackedLocation>();
        await foreach (var location in _locations.ReadAsync(cancellationToken))
            locations.Add(location);

        var observations = new List<PresenceObservationRecord>();
        await foreach (var observation in _observations.ReadAllAsync(cancellationToken))
            observations.Add(observation);

        var replay = PresenceReplay.Replay(locations, observations);
        await _events.ReplaceAsync(replay.Events, cancellationToken);
        await _states.ReplaceAsync(replay.States, cancellationToken);
    }
}
