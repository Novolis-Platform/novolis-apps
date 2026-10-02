namespace Novolis.Ndjson;

public interface INdjsonDocument : IAsyncDisposable
{
    FileInfo File { get; }

    long RecordCount { get; }

    Task<NdjsonSlice> ReadAsync(
        long skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
