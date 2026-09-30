namespace PresenceLedger.Core;

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
