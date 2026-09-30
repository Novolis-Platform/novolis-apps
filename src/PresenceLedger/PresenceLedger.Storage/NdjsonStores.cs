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

        var now = DateTimeOffset.UtcNow;
        foreach (var location in revisions
                     .GroupBy(location => location.Id)
                     .Select(group => group
                         .Where(location =>
                             (location.EffectiveFromUtc is null
                                 || location.EffectiveFromUtc <= now)
                             && (location.EffectiveToUtc is null
                                 || now < location.EffectiveToUtc))
                         .OrderBy(location =>
                             location.EffectiveFromUtc ?? DateTimeOffset.MinValue)
                         .LastOrDefault())
                     .Where(location => location is not null)
                     .Select(location => location!))
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
