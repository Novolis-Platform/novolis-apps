using System.Runtime.CompilerServices;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>NDJSON operational state store with last-write-wins snapshots.</summary>
public sealed class NdjsonPresenceStateStore : IPresenceStateStore
{
    readonly NdjsonFile _file;

    /// <summary>Creates a store at the supplied file path.</summary>
    public NdjsonPresenceStateStore(string path) => _file = new(path);

    /// <summary>Physical NDJSON path.</summary>
    public string FilePath => _file.Path;

    /// <inheritdoc />
    public async ValueTask<LocationPresenceState?> GetAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        LocationPresenceState? latest = null;
        await foreach (var state in _file.ReadAsync<LocationPresenceState>(cancellationToken))
        {
            if (state.LocationId == locationId)
                latest = state;
        }

        return latest;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LocationPresenceState> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var latest = new Dictionary<Guid, LocationPresenceState>();
        await foreach (var state in _file.ReadAsync<LocationPresenceState>(cancellationToken))
            latest[state.LocationId] = state;

        foreach (var state in latest.Values)
            yield return state;
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(
        LocationPresenceState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _file.AppendAsync(state, cancellationToken);
    }
}
