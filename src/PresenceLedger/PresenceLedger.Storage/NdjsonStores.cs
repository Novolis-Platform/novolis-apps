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
        var revisions = new List<TrackedLocation>();
        await foreach (var location in ReadHistoryAsync(cancellationToken))
            revisions.Add(location);

        foreach (var location in revisions
                     .GroupBy(item => item.Id)
                     .Select(group => group.Last()))
            yield return location;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TrackedLocation> ReadHistoryAsync(
        CancellationToken cancellationToken = default) =>
        _file.ReadAsync<TrackedLocation>(cancellationToken);

    /// <inheritdoc />
    public ValueTask SaveAsync(
        TrackedLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _file.AppendAsync(location, cancellationToken);
    }
}
