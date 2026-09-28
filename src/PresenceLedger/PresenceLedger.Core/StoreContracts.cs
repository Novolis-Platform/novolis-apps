namespace PresenceLedger.Core;

/// <summary>Reads and writes configured tracked locations.</summary>
public interface ITrackedLocationStore
{
    /// <summary>Reads the current configured locations.</summary>
    IAsyncEnumerable<TrackedLocation> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces a configured location.</summary>
    ValueTask SaveAsync(
        TrackedLocation location,
        CancellationToken cancellationToken = default);
}

/// <summary>Appends and reads semantic presence events.</summary>
public interface IPresenceEventStore
{
    /// <summary>Reads historical events in ledger order.</summary>
    IAsyncEnumerable<PresenceEvent> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Appends one semantic event.</summary>
    ValueTask AppendAsync(
        PresenceEvent presenceEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists operational inference state.</summary>
public interface IPresenceStateStore
{
    /// <summary>Reads the latest state for a location.</summary>
    ValueTask<LocationPresenceState?> GetAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the latest state for each location.</summary>
    IAsyncEnumerable<LocationPresenceState> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Appends a new state snapshot.</summary>
    ValueTask SaveAsync(
        LocationPresenceState state,
        CancellationToken cancellationToken = default);
}

/// <summary>Feeds platform observations into the inference engine.</summary>
public interface IPresenceEngine
{
    /// <summary>Processes one observation against every configured location.</summary>
    ValueTask ProcessAsync(
        Observation observation,
        CancellationToken cancellationToken = default);
}
