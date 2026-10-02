using Novolis.Storage.Ndjson;

namespace PresenceLedger.Storage;

/// <summary>Small serialized append-only JSON-lines file.</summary>
internal sealed class NdjsonFile
{
    readonly NdjsonStore _store;

    public NdjsonFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        _store = new NdjsonStore(
            path,
            new NdjsonStoreOptions(NdjsonJson.Options));
    }

    public string Path { get; }

    public async ValueTask AppendAsync<T>(
        T value,
        CancellationToken cancellationToken = default)
        => await _store.AppendAsync(value, cancellationToken).ConfigureAwait(false);

    public async ValueTask ReplaceAsync<T>(
        IEnumerable<T> values,
        CancellationToken cancellationToken = default)
        => await _store.ReplaceAsync(values, cancellationToken).ConfigureAwait(false);

    public IAsyncEnumerable<T> ReadAsync<T>(
        CancellationToken cancellationToken = default)
        => _store.ReadAsync<T>(cancellationToken);
}
