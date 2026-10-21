using Microsoft.Maui.Storage;
using Novolis.Transports.Torrent;

namespace Novolis.Torrent;

public sealed class TorrentTransferService : IDisposable
{
    private readonly object _gate = new();
    private readonly HttpClient _httpClient;
    private readonly string _torrentDirectory;
    private readonly string _downloadDirectory;
    private readonly List<TorrentTransferItem> _items = [];
    private readonly Timer _pollTimer;
    private TorrentClient? _client;
    private bool _disposed;

    public TorrentTransferService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _torrentDirectory = Path.Combine(FileSystem.AppDataDirectory, "torrents");
        _downloadDirectory = Path.Combine(FileSystem.AppDataDirectory, "downloads");
        Directory.CreateDirectory(_torrentDirectory);
        Directory.CreateDirectory(_downloadDirectory);
        _pollTimer = new Timer(Poll, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public event EventHandler? Changed;

    public string DownloadDirectory => _downloadDirectory;

    public IReadOnlyList<TorrentTransferItem> Items
    {
        get
        {
            lock (_gate)
                return _items.ToArray();
        }
    }

    public async Task<TorrentTransferItem> AddFileAsync(
        FileResult file,
        CancellationToken cancellationToken = default)
    {
        return await AddFileAsync(
            file,
            Array.Empty<Uri>(),
            cancellationToken);
    }

    public async Task<TorrentTransferItem> AddFileAsync(
        FileResult file,
        IReadOnlyList<Uri> trackers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(trackers);
        ThrowIfDisposed();

        await using var source = await file.OpenReadAsync();
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "selected.torrent";

        return await AddBytesAsync(
            fileName,
            source,
            trackers,
            cancellationToken);
    }

    public async Task<TorrentTransferItem> AddUriAsync(
        Uri torrentUri,
        CancellationToken cancellationToken = default)
    {
        return await AddUriAsync(
            torrentUri,
            Array.Empty<Uri>(),
            cancellationToken);
    }

    public async Task<TorrentTransferItem> AddUriAsync(
        Uri torrentUri,
        IReadOnlyList<Uri> trackers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(torrentUri);
        ArgumentNullException.ThrowIfNull(trackers);
        if (torrentUri.Scheme is not ("http" or "https"))
            throw new ArgumentException(
                "Torrent downloads must use HTTP or HTTPS.",
                nameof(torrentUri));

        ThrowIfDisposed();
        using var response = await _httpClient.GetAsync(
            torrentUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var fileName = Path.GetFileName(torrentUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "downloaded.torrent";

        return await AddBytesAsync(
            fileName,
            stream,
            trackers,
            cancellationToken);
    }

    public Task StartAsync(
        TorrentTransferItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (item.IsRunning)
                        return;

                    EnsureClient();
                    _client!.Start(item.Torrent);
                    item.SetRunning(true, "Checking");
                }

                Changed?.Invoke(this, EventArgs.Empty);
            },
            cancellationToken);
    }

    public Task StopAsync(
        TorrentTransferItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!item.IsRunning)
                        return;

                    if (_client?.IsRunning == true)
                        _client.Stop(item.Torrent.InfoHash);
                    item.SetRunning(false, "Stopped");
                }

                Changed?.Invoke(this, EventArgs.Empty);
            },
            cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var item in _items.Where(item => item.IsRunning))
            {
                try
                {
                    if (_client?.IsRunning == true)
                        _client.Stop(item.Torrent.InfoHash);
                }
                catch
                {
                    // App shutdown is best effort; the process owns the sockets.
                }

                item.SetRunning(false, "Stopped");
            }

            _client?.Dispose();
            _client = null;
        }

        _pollTimer.Dispose();
    }

    private async Task<TorrentTransferItem> AddBytesAsync(
        string fileName,
        Stream source,
        IReadOnlyList<Uri> trackers,
        CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        if (!safeName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            safeName += ".torrent";

        var path = Path.Combine(
            _torrentDirectory,
            $"{Guid.NewGuid():N}-{safeName}");
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        var torrentBytes = buffer.ToArray();

        if (!TorrentInfo.TryLoad(torrentBytes, out var originalTorrent)
            || originalTorrent is null)
        {
            throw new InvalidDataException("The selected file is not a valid torrent.");
        }

        if (!originalTorrent.IsPrivate && trackers.Count > 0)
        {
            torrentBytes = TorrentTrackerEnricher.AddToTorrent(
                torrentBytes,
                trackers);
        }

        await File.WriteAllBytesAsync(path, torrentBytes, cancellationToken);
        if (!TorrentInfo.TryLoad(torrentBytes, out var torrent) || torrent is null)
        {
            File.Delete(path);
            throw new InvalidDataException("The enriched torrent is not valid.");
        }

        var item = new TorrentTransferItem(
            torrent,
            path,
            _downloadDirectory);
        lock (_gate)
            _items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    private void EnsureClient()
    {
        if (_client is null)
        {
            _client = new TorrentClient(6881, _downloadDirectory);
            _client.Start();
        }
        else if (!_client.IsRunning)
        {
            _client.Start();
        }
    }

    private void Poll(object? state)
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_client?.IsRunning != true)
                return;

            foreach (var item in _items.Where(item => item.IsRunning))
            {
                try
                {
                    var progress = _client.GetProgressInfo(item.Torrent.InfoHash);
                    item.SetProgress(progress, GetStatus(progress));
                }
                catch (Exception exception)
                {
                    item.SetProgress(item.Progress, $"Error: {exception.Message}");
                }
            }
        }

    }

    private static string GetStatus(TorrentProgressInfo? progress)
    {
        if (progress is null)
            return "Waiting";
        if (progress.CompletedPercentage >= 100m)
            return progress.UploadSpeed > 0 ? "Seeding" : "Complete";
        if (progress.DownloadSpeed > 0)
            return "Downloading";
        return "Waiting for peers";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TorrentTransferService));
    }
}
