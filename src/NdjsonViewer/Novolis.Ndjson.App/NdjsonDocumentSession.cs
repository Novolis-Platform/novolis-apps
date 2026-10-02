using Novolis.Ndjson;

namespace Novolis.Ndjson.App;

public sealed class NdjsonDocumentSession(NdjsonFileReader reader) : IAsyncDisposable
{
    private INdjsonDocument? _document;
    private NdjsonOpenRequest? _request;
    private FileInfo? _cacheFile;

    public INdjsonDocument? Document => _document;

    public string? DisplayName => _request?.DisplayName;

    public FileInfo? CurrentFile => _document?.File;

    public async Task OpenAsync(
        NdjsonOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PhysicalFile is null && request.OpenReadAsync is null)
            throw new ArgumentException("A physical file or stream factory is required.", nameof(request));

        await CloseDocumentAsync().ConfigureAwait(false);
        _request = request;

        var file = await MaterializeIfNeededAsync(cancellationToken).ConfigureAwait(false);
        _document = await reader.OpenAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var document = _document ?? throw new InvalidOperationException("No NDJSON document is open.");
        if (_request?.PhysicalFile is not null)
        {
            await document.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var file = await MaterializeIfNeededAsync(cancellationToken).ConfigureAwait(false);
        await document.DisposeAsync().ConfigureAwait(false);
        _document = await reader.OpenAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseDocumentAsync().ConfigureAwait(false);
        if (_cacheFile is { } cacheFile)
        {
            try
            {
                cacheFile.Delete();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private async Task<FileInfo> MaterializeIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_request?.PhysicalFile is { } physicalFile)
            return new FileInfo(Path.GetFullPath(physicalFile.FullName));

        var request = _request
            ?? throw new InvalidOperationException("No NDJSON document is open.");
        var openReadAsync = request.OpenReadAsync
            ?? throw new InvalidOperationException("The document has no readable source.");

        var directory = Path.Combine(FileSystem.Current.CacheDirectory, "ndjson");
        Directory.CreateDirectory(directory);
        _cacheFile ??= new FileInfo(Path.Combine(directory, $"{Guid.NewGuid():N}.ndjson"));

        var temporaryPath = $"{_cacheFile.FullName}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using var source = await openReadAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = new FileStream(
                temporaryPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.Read,
                    BufferSize = 64 * 1024,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });
            await source.CopyToAsync(destination, 64 * 1024, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _cacheFile.FullName, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
        }

        return _cacheFile;
    }

    private async ValueTask CloseDocumentAsync()
    {
        if (_document is { } document)
        {
            _document = null;
            await document.DisposeAsync().ConfigureAwait(false);
        }
    }
}
