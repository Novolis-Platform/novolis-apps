using System.Runtime.CompilerServices;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

/// <summary>NDJSON location definition store with last-write-wins snapshots.</summary>
public sealed class NdjsonTrackedLocationStore : ITrackedLocationStore
{
    readonly NdjsonFile _file;

    /// <summary>Creates a store at the supplied file path.</summary>
    public NdjsonTrackedLocationStore(string path) => _file = new(path);

    /// <summary>Physical NDJSON path.</summary>
    public string FilePath => _file.Path;

    /// <inheritdoc />
    public async IAsyncEnumerable<TrackedLocation> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var latest = new Dictionary<Guid, TrackedLocation>();
        await foreach (var location in _file.ReadAsync<TrackedLocation>(cancellationToken))
            latest[location.Id] = location;

        foreach (var location in latest.Values)
            yield return location;
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(
        TrackedLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _file.AppendAsync(location, cancellationToken);
    }
}

/// <summary>NDJSON semantic presence event store.</summary>
public sealed class NdjsonPresenceEventStore : IPresenceEventStore
{
    readonly NdjsonFile _file;

    /// <summary>Creates a store at the supplied file path.</summary>
    public NdjsonPresenceEventStore(string path) => _file = new(path);

    /// <summary>Physical NDJSON path.</summary>
    public string FilePath => _file.Path;

    /// <inheritdoc />
    public IAsyncEnumerable<PresenceEvent> ReadAsync(
        CancellationToken cancellationToken = default) =>
        _file.ReadAsync<PresenceEvent>(cancellationToken);

    /// <inheritdoc />
    public ValueTask AppendAsync(
        PresenceEvent presenceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presenceEvent);
        return _file.AppendAsync(presenceEvent, cancellationToken);
    }
}

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

/// <summary>Convenience bundle of the three first-version local stores.</summary>
public sealed class NdjsonPresenceStorage
{
    /// <summary>Creates stores beneath an application-private root.</summary>
    public NdjsonPresenceStorage(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Directory.CreateDirectory(rootDirectory);
        Locations = new NdjsonTrackedLocationStore(
            System.IO.Path.Combine(rootDirectory, "locations.ndjson"));
        Events = new NdjsonPresenceEventStore(
            System.IO.Path.Combine(rootDirectory, "presence.ndjson"));
        States = new NdjsonPresenceStateStore(
            System.IO.Path.Combine(rootDirectory, "states.ndjson"));
    }

    /// <summary>Location definition store.</summary>
    public NdjsonTrackedLocationStore Locations { get; }

    /// <summary>Semantic event store.</summary>
    public NdjsonPresenceEventStore Events { get; }

    /// <summary>Operational inference state store.</summary>
    public NdjsonPresenceStateStore States { get; }
}
