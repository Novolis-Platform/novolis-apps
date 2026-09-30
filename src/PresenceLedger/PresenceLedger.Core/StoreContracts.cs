namespace PresenceLedger.Core;

/// <summary>Reads and writes configured tracked locations.</summary>
public interface ITrackedLocationStore
{
    /// <summary>Reads the current configured locations.</summary>
    IAsyncEnumerable<TrackedLocation> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Reads all retained location revisions in append order.</summary>
    IAsyncEnumerable<TrackedLocation> ReadHistoryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces a configured location.</summary>
    ValueTask SaveAsync(
        TrackedLocation location,
        CancellationToken cancellationToken = default);
}
