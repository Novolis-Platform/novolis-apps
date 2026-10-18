using System.Net;
using System.Net.Sockets;
using System.Text;
using Novolis.Transports.Ed2k;
using Novolis.Transports.Kad;

namespace Novolis.NMule.Core;

/// <summary>
/// Owns the foreground nMule eD2K search, Kad source lookup, and bounded
/// one-part download workflow.
/// </summary>
public sealed class NmuleSession : IAsyncDisposable
{
    private readonly INmuleNetworkGate _networkGate;
    private readonly NmuleStoragePaths _storage;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private Ed2kSession? _ed2k;
    private KadNode? _kad;
    private IReadOnlyList<KadContact> _kadContacts = [];
    private int _disposed;

    /// <summary>Creates a session over the supplied network gate and storage.</summary>
    public NmuleSession(
        INmuleNetworkGate? networkGate = null,
        NmuleStoragePaths? storage = null,
        HttpClient? httpClient = null)
    {
        _networkGate = networkGate ?? new AllowAllNmuleNetworkGate();
        _storage = storage
            ?? NmuleStoragePaths.FromRoot(
                Path.Combine(Path.GetTempPath(), "Novolis", "nMule"));
        Directory.CreateDirectory(_storage.CompletedDirectory);

        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        _ownsHttpClient = httpClient is null;
    }

    /// <summary>Raised for bounded user-facing workflow status.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Gets the network policy supplied by the host.</summary>
    public INmuleNetworkGate NetworkGate => _networkGate;

    /// <summary>Gets the last successfully loaded server list.</summary>
    public ServerList? ServerList { get; private set; }

    /// <summary>Gets the active eD2K server connection, if any.</summary>
    public Ed2kConnectionInfo? ServerConnection => _ed2k?.Connection;

    /// <summary>Gets whether a Kad UDP node is active.</summary>
    public bool IsKadConnected => _kad is not null;

    /// <summary>Gets the currently known Kad contacts.</summary>
    public IReadOnlyList<KadContact> KadContacts => _kadContacts;

    /// <summary>Loads and caches a text or server.met list.</summary>
    public async Task<ServerList> LoadServerListAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var bytes = await LoadSourceBytesAsync(
                source,
                _storage.ServerListPath,
                cancellationToken)
            .ConfigureAwait(false);
        var list = ParseServerList(bytes);
        if (list.Servers.Count == 0)
            throw new InvalidDataException("The server list has no usable endpoints.");

        ServerList = list;
        RaiseStatus($"Loaded {list.Servers.Count} eD2K server endpoint(s).");
        return list;
    }

    /// <summary>
    /// Connects to one explicitly selected eD2K endpoint.
    /// </summary>
    public Task<Ed2kConnectionInfo> ConnectServerAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        return ConnectServerAsync(
            new ServerList(
            [
                new Ed2kServerInfo(
                    new Ed2kServerEndpoint(host.Trim(), port)),
            ]),
            cancellationToken);
    }

    /// <summary>Connects to the first reachable endpoint in the loaded list.</summary>
    public async Task<Ed2kConnectionInfo> ConnectServerAsync(
        ServerList? serverList = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var list = serverList ?? ServerList
            ?? throw new InvalidOperationException("Load an eD2K server list first.");
        await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);

        await DisposeEd2kAsync().ConfigureAwait(false);
        var session = new Ed2kSession(clientName: "nMule Android");
        Exception? lastFailure = null;
        foreach (var server in list.Servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureNetworkAllowedAsync(
                    TryParseAddress(server.Endpoint.Host),
                    cancellationToken)
                .ConfigureAwait(false);
            try
            {
                RaiseStatus($"Connecting to {server.Endpoint}...");
                var connection = await session.ConnectServerAsync(
                        server.Endpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
                _ed2k = session;
                RaiseStatus(
                    $"Connected to {server.Endpoint} "
                    + $"({(connection.IsHighId ? "HighID" : "LowID")}).");
                return connection;
            }
            catch (Exception exception) when (
                exception is SocketException
                    or IOException
                    or InvalidDataException
                    or TimeoutException)
            {
                lastFailure = exception;
            }
        }

        await session.DisposeAsync().ConfigureAwait(false);
        throw new InvalidOperationException(
            "No eD2K server accepted the connection.",
            lastFailure);
    }

    /// <summary>Searches the connected eD2K server by name.</summary>
    public async Task<IReadOnlyList<NmuleSearchHit>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var session = _ed2k
            ?? throw new InvalidOperationException("Connect to an eD2K server first.");
        await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);
        RaiseStatus($"Searching for {query.Trim()}...");
        var results = await session.SearchAsync(query.Trim(), cancellationToken)
            .ConfigureAwait(false);
        var hits = results.Select(static result => new NmuleSearchHit(result)).ToArray();
        RaiseStatus($"Search returned {hits.Length} result(s).");
        return hits;
    }

    /// <summary>
    /// Downloads and caches a nodes.dat file, then bootstraps Kad from its
    /// first reachable contacts.
    /// </summary>
    public async Task<IReadOnlyList<KadContact>> BootstrapKadAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var bytes = await LoadSourceBytesAsync(
                source,
                _storage.KadNodesPath,
                cancellationToken)
            .ConfigureAwait(false);
        var contacts = NmuleNodesDat.Parse(bytes);
        if (contacts.Count == 0)
            throw new InvalidDataException("The nodes.dat file has no usable Kad contacts.");

        await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);
        await DisposeKadAsync().ConfigureAwait(false);
        var node = new KadNode(
            udpPort: 0,
            tcpPort: _ed2k?.ListenPort ?? 4662);
        Exception? lastFailure = null;
        var known = new Dictionary<string, KadContact>(StringComparer.Ordinal);
        foreach (var contact in contacts.Take(12))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsureNetworkAllowedAsync(
                        contact.Endpoint.Address,
                        cancellationToken)
                    .ConfigureAwait(false);
                RaiseStatus($"Bootstrapping Kad via {contact.Endpoint}...");
                var discovered = await node.BootstrapAsync(
                        contact,
                        cancellationToken)
                    .ConfigureAwait(false);
                AddContacts(known, discovered);
                AddContacts(known, [contact]);
                if (discovered.Count > 0)
                    break;
            }
            catch (Exception exception) when (
                exception is SocketException
                    or IOException
                    or InvalidDataException
                    or TimeoutException)
            {
                lastFailure = exception;
            }
        }

        if (known.Count == 0)
        {
            await node.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                "No Kad bootstrap contact responded.",
                lastFailure);
        }

        _kad = node;
        _kadContacts = known.Values.ToArray();
        RaiseStatus($"Kad is ready with {_kadContacts.Count} known contact(s).");
        return _kadContacts;
    }

    /// <summary>Looks up additional eD2K sources through the active Kad table.</summary>
    public async Task<IReadOnlyList<KadSource>> FindKadSourcesAsync(
        NmuleSearchHit hit,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(hit);
        var node = _kad
            ?? throw new InvalidOperationException("Bootstrap Kad before looking up sources.");
        await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);

        var target = new KadId(hit.FileId.Bytes);
        var contacts = _kadContacts
            .Concat(node.RoutingTable.FindClosest(target))
            .GroupBy(static contact => contact.Endpoint.ToString(), StringComparer.Ordinal)
            .Select(static group => group.First())
            .Take(12)
            .ToArray();
        var sources = new Dictionary<string, KadSource>(StringComparer.Ordinal);
        foreach (var contact in contacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureNetworkAllowedAsync(
                    contact.Endpoint.Address,
                    cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var discovered = await node.FindSourcesAsync(
                        hit.FileId,
                        contact,
                        cancellationToken)
                    .ConfigureAwait(false);
                foreach (var source in discovered)
                    sources[SourceKey(source)] = source;
            }
            catch (Exception) when (
                !cancellationToken.IsCancellationRequested
                && (contacts.Length > 1))
            {
                // One stale Kad contact must not hide the remaining routing table.
            }
        }

        return sources.Values.ToArray();
    }

    /// <summary>
    /// Downloads one bounded file into app-private storage, using a direct
    /// HighID source or a Kad source when required.
    /// </summary>
    public async Task<NmuleDownloadResult> DownloadAsync(
        NmuleSearchHit hit,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(hit);
        if (hit.Eligibility is NmuleTransferEligibility.MultipartRequired
            or NmuleTransferEligibility.InvalidSource)
        {
            throw new NotSupportedException(
                $"{hit.Name} cannot be downloaded by the nMule MVP: "
                + $"{hit.EligibilityLabel}.");
        }

        var session = _ed2k
            ?? throw new InvalidOperationException("Connect to an eD2K server first.");
        await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_storage.CompletedDirectory);

        var fileName = MakeSafeFileName(hit.Name, hit.FileId.ToString());
        var destination = GetUniqueDestination(fileName);
        var temporary = $"{destination}.{Guid.NewGuid():N}.partial";
        try
        {
            string sourceDescription;
            if (hit.Eligibility == NmuleTransferEligibility.DirectHighId)
            {
                var address = ClientIdToAddress(hit.Result.ClientId);
                await EnsureNetworkAllowedAsync(address, cancellationToken)
                    .ConfigureAwait(false);
                RaiseStatus($"Downloading {fileName} from the eD2K source...");
                await session.DownloadAsync(
                        hit.Result,
                        temporary,
                        cancellationToken)
                    .ConfigureAwait(false);
                sourceDescription = "eD2K HighID";
            }
            else
            {
                var sources = await FindKadSourcesAsync(hit, cancellationToken)
                    .ConfigureAwait(false);
                if (sources.Count == 0)
                {
                    throw new NotSupportedException(
                        "No usable Kad source was returned for this LowID result.");
                }

                Exception? lastFailure = null;
                sourceDescription = string.Empty;
                foreach (var source in sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await EnsureNetworkAllowedAsync(
                            source.Endpoint.Address,
                            cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        RaiseStatus(
                            $"Downloading {fileName} from Kad source "
                            + $"{source.Endpoint.Address}:{source.TcpPort}...");
                        await Ed2kPeerSession.DownloadAsync(
                                new IPEndPoint(
                                    source.Endpoint.Address,
                                    source.TcpPort),
                                hit.FileId,
                                hit.Size,
                                temporary,
                                session.UserHash,
                                session.ListenPort,
                                cancellationToken)
                            .ConfigureAwait(false);
                        sourceDescription =
                            $"Kad source {source.Endpoint.Address}:{source.TcpPort}";
                        break;
                    }
                    catch (Exception exception) when (
                        exception is SocketException
                            or IOException
                            or InvalidDataException
                            or TimeoutException)
                    {
                        lastFailure = exception;
                    }
                }

                if (sourceDescription.Length == 0)
                {
                    throw new InvalidOperationException(
                        "All Kad sources failed the one-part transfer.",
                        lastFailure);
                }
            }

            File.Move(temporary, destination);
            RaiseStatus($"Completed {fileName}.");
            return new NmuleDownloadResult(
                fileName,
                destination,
                hit.Size,
                sourceDescription);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>Disconnects eD2K and Kad without deleting cached data.</summary>
    public async Task DisconnectAsync()
    {
        await DisposeEd2kAsync().ConfigureAwait(false);
        await DisposeKadAsync().ConfigureAwait(false);
        _kadContacts = [];
        RaiseStatus("Disconnected from eD2K and Kad.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await DisposeEd2kAsync().ConfigureAwait(false);
        await DisposeKadAsync().ConfigureAwait(false);
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private async Task<byte[]> LoadSourceBytesAsync(
        string source,
        string cachePath,
        CancellationToken cancellationToken)
    {
        var trimmedSource = source.Trim();
        var isWindowsPath = trimmedSource.Length >= 2
            && trimmedSource[1] == ':';
        if (Uri.TryCreate(trimmedSource, UriKind.Absolute, out var uri)
            && !uri.IsFile
            && !isWindowsPath)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "nMule bootstrap sources must use HTTPS.",
                    nameof(source));
            }

            await EnsureNetworkAllowedAsync(null, cancellationToken).ConfigureAwait(false);
            RaiseStatus($"Fetching {uri.Host}...");
            try
            {
                using var response = await _httpClient.GetAsync(
                        uri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var bytes = await ReadBoundedAsync(
                        response,
                        NmuleDefaults.MaximumBootstrapBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                await CacheBytesAsync(cachePath, bytes, cancellationToken)
                    .ConfigureAwait(false);
                return bytes;
            }
            catch (HttpRequestException) when (File.Exists(cachePath))
            {
                RaiseStatus("Bootstrap source unavailable; using the cached copy.");
                return await File.ReadAllBytesAsync(cachePath, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        source = trimmedSource;
        if (string.Equals(trimmedSource, "cache", StringComparison.OrdinalIgnoreCase))
            source = cachePath;
        if (!File.Exists(source))
            throw new FileNotFoundException(
                "The bootstrap source was not found.",
                source);
        return await File.ReadAllBytesAsync(source, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ServerList ParseServerList(byte[] bytes)
    {
        if (bytes.Length > 0 && bytes[0] is 0xE0 or 0x0E)
            return ServerList.ParseServerMet(bytes);
        return ServerList.ParseText(
            Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
    }

    private async Task EnsureNetworkAllowedAsync(
        IPAddress? endpointAddress,
        CancellationToken cancellationToken)
    {
        var decision = await _networkGate.CheckAsync(
                endpointAddress,
                cancellationToken)
            .ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            throw new NmuleNetworkBlockedException(
                decision.DenialReason
                ?? "nMule network access is blocked by policy.");
        }
    }

    private async Task DisposeEd2kAsync()
    {
        var session = Interlocked.Exchange(ref _ed2k, null);
        if (session is not null)
            await session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DisposeKadAsync()
    {
        var node = Interlocked.Exchange(ref _kad, null);
        if (node is not null)
            await node.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpResponseMessage response,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException("The bootstrap payload is too large.");

        await using var input = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException("The bootstrap payload is too large.");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static async Task CacheBytesAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = $"{path}.{Guid.NewGuid():N}.cache";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static IPAddress? TryParseAddress(string host) =>
        IPAddress.TryParse(host, out var address) ? address : null;

    private static IPAddress ClientIdToAddress(uint clientId) =>
        new(
        [
            (byte)(clientId & 0xFF),
            (byte)((clientId >> 8) & 0xFF),
            (byte)((clientId >> 16) & 0xFF),
            (byte)((clientId >> 24) & 0xFF),
        ]);

    private string GetUniqueDestination(string fileName)
    {
        var candidate = Path.Combine(_storage.CompletedDirectory, fileName);
        if (!File.Exists(candidate))
            return candidate;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 2; index < 10_000; index++)
        {
            candidate = Path.Combine(
                _storage.CompletedDirectory,
                $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(
            _storage.CompletedDirectory,
            $"{stem} ({Guid.NewGuid():N}){extension}");
    }

    private static string MakeSafeFileName(string name, string fallback)
    {
        var fileName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
            fileName = $"{fallback}.bin";
        foreach (var invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');
        return fileName;
    }

    private static string SourceKey(KadSource source) =>
        $"{source.Endpoint.Address}:{source.TcpPort}";

    private static void AddContacts(
        IDictionary<string, KadContact> destination,
        IEnumerable<KadContact> contacts)
    {
        foreach (var contact in contacts)
            destination[contact.Endpoint.ToString()] = contact;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void RaiseStatus(string status) => StatusChanged?.Invoke(status);

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
}
