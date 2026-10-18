using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PresenceLedger.Storage;

/// <summary>Creates and restores a versioned, integrity-checked ledger archive.</summary>
public sealed class LedgerBundleService
{
    public const string Schema = "novolis.presence-ledger.bundle";
    public const int Version = 1;

    const string ManifestEntryName = "manifest.json";
    const string FilesPrefix = "files/";
    const long MaximumFileBytes = 64 * 1024 * 1024;
    const long MaximumBundleBytes = 512 * 1024 * 1024;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    readonly NdjsonPresenceStorage _storage;

    /// <summary>Creates an archive service over a local storage bundle.</summary>
    public LedgerBundleService(NdjsonPresenceStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>Builds a complete ledger archive into an already-open stream.</summary>
    public async Task<LedgerBundleResult> CreateAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));

        await _storage.BuildCumulativeAsync(cancellationToken).ConfigureAwait(false);
        var files = LedgerFiles.List(_storage.RootDirectory);
        var manifestFiles = new List<LedgerBundleFile>(files.Count);
        var totalBytes = 0L;

        using (var archive = new ZipArchive(
                   destination,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.LengthBytes > MaximumFileBytes)
                {
                    throw new InvalidDataException(
                        $"Ledger file '{file.RelativePath}' exceeds the archive limit.");
                }

                totalBytes = checked(totalBytes + file.LengthBytes);
                if (totalBytes > MaximumBundleBytes)
                    throw new InvalidDataException("The ledger is too large to export.");

                var entry = archive.CreateEntry(
                    FilesPrefix + file.RelativePath,
                    CompressionLevel.Fastest);
                await using var input = new FileStream(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan);
                await using var output = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)
                           .ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                }

                manifestFiles.Add(new LedgerBundleFile
                {
                    Path = file.RelativePath,
                    Length = file.LengthBytes,
                    Sha256 = Convert.ToHexString(hash.GetHashAndReset()),
                });
            }

            var manifest = new LedgerBundleManifest
            {
                Schema = Schema,
                Version = Version,
                CreatedUtc = DateTimeOffset.UtcNow,
                Files = manifestFiles,
            };
            var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Fastest);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                    manifestStream,
                    manifest,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        return new LedgerBundleResult(manifestFiles.Count, totalBytes);
    }

    /// <summary>Builds an archive atomically at a normal filesystem path.</summary>
    public async Task<LedgerBundleResult> CreateToPathAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        archivePath = Path.GetFullPath(archivePath);
        var parent = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var temporaryPath = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            LedgerBundleResult result;
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.SequentialScan))
            {
                result = await CreateAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, archivePath, overwrite: true);
            return result;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Validates and replaces the complete ledger from an archive stream.</summary>
    public async Task<LedgerBundleImportResult> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("The source stream must be readable.", nameof(source));

        var archivePath = Path.Combine(
            Path.GetTempPath(),
            "Novolis-PresenceLedger-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await using (var archive = new FileStream(
                             archivePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.SequentialScan))
            {
                await source.CopyToAsync(archive, cancellationToken).ConfigureAwait(false);
                await archive.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return await ImportFromPathAsync(archivePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }

    /// <summary>Validates and replaces the complete ledger from an archive path.</summary>
    public async Task<LedgerBundleImportResult> ImportFromPathAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        archivePath = Path.GetFullPath(archivePath);
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("The ledger archive was not found.", archivePath);

        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "Novolis-PresenceLedger-Import-" + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(
            Path.GetTempPath(),
            "Novolis-PresenceLedger-Backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);
        Directory.CreateDirectory(backupRoot);
        try
        {
            var manifest = await ExtractAndValidateAsync(
                    archivePath,
                    extractionRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            var extractedLedgerRoot = Path.Combine(extractionRoot, "files");
            var currentFiles = LedgerFiles.List(_storage.RootDirectory);
            CopyFiles(currentFiles, _storage.RootDirectory, backupRoot);

            try
            {
                DeleteFiles(currentFiles);
                var importedFiles = LedgerFiles.List(extractedLedgerRoot);
                CopyFiles(importedFiles, extractedLedgerRoot, _storage.RootDirectory);
            }
            catch
            {
                DeleteFiles(LedgerFiles.List(_storage.RootDirectory));
                CopyFiles(LedgerFiles.List(backupRoot), backupRoot, _storage.RootDirectory);
                throw;
            }

            return new LedgerBundleImportResult(manifest.Files.Count, manifest.Files.Sum(file => file.Length));
        }
        finally
        {
            DeleteDirectoryBestEffort(extractionRoot);
            DeleteDirectoryBestEffort(backupRoot);
        }
    }

    async Task<LedgerBundleManifest> ExtractAndValidateAsync(
        string archivePath,
        string extractionRoot,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("The ledger archive has no manifest.");
        if (manifestEntry.Length > 1024 * 1024)
            throw new InvalidDataException("The ledger manifest is too large.");

        LedgerBundleManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            try
            {
                manifest = await JsonSerializer.DeserializeAsync<LedgerBundleManifest>(
                        manifestStream,
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The ledger manifest is not valid JSON.", exception);
            }
        }

        ValidateManifest(manifest);
        var expected = manifest!.Files.ToDictionary(
            file => FilesPrefix + file.Path,
            StringComparer.Ordinal);
        var archiveFiles = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .ToArray();
        if (archiveFiles.Length != expected.Count + 1
            || archiveFiles.Any(entry =>
                !string.Equals(entry.FullName, ManifestEntryName, StringComparison.Ordinal)
                && !expected.ContainsKey(entry.FullName)))
        {
            throw new InvalidDataException("The ledger archive contains unexpected files.");
        }

        var totalBytes = 0L;
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(FilesPrefix + file.Path)
                ?? throw new InvalidDataException($"The ledger file '{file.Path}' is missing.");
            if (entry.Length != file.Length || entry.Length > MaximumFileBytes)
                throw new InvalidDataException($"The ledger file '{file.Path}' has an invalid length.");

            totalBytes = checked(totalBytes + entry.Length);
            if (totalBytes > MaximumBundleBytes)
                throw new InvalidDataException("The ledger archive is too large.");

            var destination = SafeCombine(extractionRoot, FilesPrefix + file.Path);
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            await using (var entryStream = entry.Open())
            await using (var output = new FileStream(
                             destination,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.SequentialScan))
            {
                await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            await using var verifyStream = File.OpenRead(destination);
            var actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(verifyStream, cancellationToken)
                    .ConfigureAwait(false));
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The ledger file '{file.Path}' failed its integrity check.");
            ValidateNdjson(destination, file.Path);
        }

        return manifest;
    }

    static void ValidateManifest(LedgerBundleManifest? manifest)
    {
        if (manifest is null
            || !string.Equals(manifest.Schema, Schema, StringComparison.Ordinal)
            || manifest.Version != Version
            || manifest.Files is null)
        {
            throw new InvalidDataException("The ledger archive manifest is unsupported.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (file is null
                || string.IsNullOrWhiteSpace(file.Path)
                || !file.Path.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)
                || !paths.Add(file.Path)
                || file.Length < 0
                || file.Length > MaximumFileBytes
                || file.Sha256 is null
                || file.Sha256.Length != 64
                || file.Sha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException("The ledger archive manifest contains an invalid file.");
            }

            SafeCombine(string.Empty, FilesPrefix + file.Path);
        }
    }

    static void ValidateNdjson(string path, string relativePath)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    throw new JsonException("A ledger line cannot be null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"The ledger file '{relativePath}' has invalid JSON on line {lineNumber}.",
                    exception);
            }
        }
    }

    static string SafeCombine(string root, string relative)
    {
        var normalized = relative.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("The ledger archive contains an unsafe path.");
        }

        var fullRoot = string.IsNullOrEmpty(root)
            ? Path.GetFullPath(Path.DirectorySeparatorChar.ToString())
            : Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(
            Path.Combine(
                string.IsNullOrEmpty(root) ? fullRoot : fullRoot,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ledger archive contains an unsafe path.");
        return fullPath;
    }

    static void CopyFiles(
        IReadOnlyList<LedgerFile> files,
        string sourceRoot,
        string destinationRoot)
    {
        foreach (var file in files)
        {
            var destination = SafeCombine(destinationRoot, file.RelativePath);
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            File.Copy(file.FullPath, destination, overwrite: true);
        }
    }

    static void DeleteFiles(IReadOnlyList<LedgerFile> files)
    {
        foreach (var file in files)
        {
            if (File.Exists(file.FullPath))
                File.Delete(file.FullPath);
        }
    }

    static void DeleteDirectoryBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
