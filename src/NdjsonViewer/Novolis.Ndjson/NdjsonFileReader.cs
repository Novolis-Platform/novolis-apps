namespace Novolis.Ndjson;

public sealed class NdjsonFileReader
{
    private readonly NdjsonOpenOptions _options;

    public NdjsonFileReader(NdjsonOpenOptions? options = null)
    {
        _options = options ?? new NdjsonOpenOptions();
        _options.Validate();
    }

    public async Task<INdjsonDocument> OpenAsync(
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        file.Refresh();
        if (!file.Exists)
            throw new FileNotFoundException("The NDJSON file was not found.", file.FullName);

        if ((file.Attributes & FileAttributes.Directory) != 0)
            throw new ArgumentException("The supplied path is a directory.", nameof(file));

        var normalizedFile = new FileInfo(Path.GetFullPath(file.FullName));
        var indexer = new NdjsonFileIndexer(_options);
        var index = await indexer.CreateInitialAsync(normalizedFile, cancellationToken).ConfigureAwait(false);
        return new NdjsonDocument(normalizedFile, _options, indexer, index);
    }
}
