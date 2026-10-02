using System.Runtime.CompilerServices;
using PresenceLedger.Core;

namespace PresenceLedger.Storage;

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

    /// <inheritdoc />
    public ValueTask ReplaceAsync(
        IReadOnlyList<PresenceEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        return _file.ReplaceAsync(events, cancellationToken);
    }
}
