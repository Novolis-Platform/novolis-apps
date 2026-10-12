using Android.Content;
using Android.Provider;
using PresenceLedger.App;
using PresenceLedger.Storage;
using System.Runtime.Versioning;

namespace PresenceLedger.Android;

/// <summary>Publishes the ledger into the public Downloads folder so My Files and a computer can read it.</summary>
public sealed class AndroidDownloadsLedgerPublisher : ILedgerFilePublisher
{
    static readonly Lock PublishGate = new();
    readonly string _sourceRoot;

    /// <summary>Creates a publisher for the private ledger root.</summary>
    public AndroidDownloadsLedgerPublisher(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        _sourceRoot = sourceRoot;
    }

    /// <inheritdoc />
    public Task<string> PublishAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            return Task.FromResult(PublishWithMediaStore(cancellationToken));

        var downloads = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryDownloads)?.AbsolutePath
            ?? throw new InvalidOperationException("Android Downloads directory is unavailable.");
        var destination = Path.Combine(downloads, "PresenceLedger");
        LedgerFiles.CopyTo(_sourceRoot, destination);
        return Task.FromResult(destination);
    }

    [SupportedOSPlatform("android29.0")]
    string PublishWithMediaStore(CancellationToken cancellationToken)
    {
        var resolver = global::Android.App.Application.Context?.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver is unavailable.");
        var collection = MediaStore.Downloads.ExternalContentUri
            ?? throw new InvalidOperationException("Android Downloads collection is unavailable.");

        lock (PublishGate)
        {
            foreach (var file in LedgerFiles.List(_sourceRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Write(resolver, collection, file);
            }
        }

        return "Downloads/PresenceLedger";
    }

    [SupportedOSPlatform("android29.0")]
    static void Write(ContentResolver resolver, global::Android.Net.Uri collection, LedgerFile file)
    {
        var displayName = Path.GetFileName(file.RelativePath);
        var directory = Path.GetDirectoryName(file.RelativePath)?.Replace('\\', '/');
        var relativePath = string.IsNullOrEmpty(directory)
            ? "Download/PresenceLedger/"
            : $"Download/PresenceLedger/{directory}/";

        var matches = FindMatches(resolver, collection, displayName, relativePath);
        var existing = matches
            .Where(match => string.Equals(
                match.DisplayName,
                displayName,
                StringComparison.Ordinal)
                && match.IsOwnedByApplication)
            .Concat(matches.Where(match =>
                !string.Equals(match.DisplayName, displayName, StringComparison.Ordinal)
                && match.IsOwnedByApplication))
            .Select(match => match.Uri)
            .FirstOrDefault();
        foreach (var match in matches)
        {
            if (match.IsOwnedByApplication
                && (existing is null || !match.Uri.Equals(existing)))
                DeleteBestEffort(resolver, match.Uri);
        }

        if (existing is not null)
        {
            // MediaProvider rejects "wt" for some already-indexed Downloads
            // rows. Its default write mode still truncates the existing row.
            using var stream = resolver.OpenOutputStream(existing)
                ?? throw new InvalidOperationException($"Android could not open {file.RelativePath}.");
            using var input = File.OpenRead(file.FullPath);
            input.CopyTo(stream);
            return;
        }

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
        values.Put(MediaStore.IMediaColumns.MimeType, "application/x-ndjson");
        values.Put(MediaStore.IMediaColumns.RelativePath, relativePath);
        var uri = resolver.Insert(collection, values)
            ?? throw new InvalidOperationException($"Android could not create {file.RelativePath}.");
        try
        {
            using var stream = resolver.OpenOutputStream(uri)
                ?? throw new InvalidOperationException($"Android could not write {file.RelativePath}.");
            using var input = File.OpenRead(file.FullPath);
            input.CopyTo(stream);
        }
        catch
        {
            resolver.Delete(uri, null, null);
            throw;
        }
    }

    [SupportedOSPlatform("android29.0")]
    static List<(global::Android.Net.Uri Uri, string DisplayName, bool IsOwnedByApplication)> FindMatches(
        ContentResolver resolver,
        global::Android.Net.Uri collection,
        string displayName,
        string relativePath)
    {
        var matches =
            new List<(global::Android.Net.Uri Uri, string DisplayName, bool IsOwnedByApplication)>();
        var relativePathColumn = MediaStore.IMediaColumns.RelativePath;
        const string ownerPackageColumn = "owner_package_name";
        var packageName = global::Android.App.Application.Context?.PackageName;
        using var cursor = resolver.Query(
            collection,
            ["_id", MediaStore.IMediaColumns.DisplayName, relativePathColumn, ownerPackageColumn],
            null,
            null,
            null);
        if (cursor is null)
            return matches;

        var idColumn = cursor.GetColumnIndexOrThrow("_id");
        var displayNameColumn = cursor.GetColumnIndexOrThrow(
            MediaStore.IMediaColumns.DisplayName);
        var relativePathIndex = cursor.GetColumnIndexOrThrow(relativePathColumn);
        var ownerPackageIndex = cursor.GetColumnIndexOrThrow(ownerPackageColumn);
        if (!cursor.MoveToFirst())
            return matches;

        do
        {
            var foundName = cursor.GetString(displayNameColumn);
            if (foundName is null)
                continue;

            if (!string.Equals(
                    cursor.GetString(relativePathIndex),
                    relativePath,
                    StringComparison.Ordinal)
                || (!string.Equals(foundName, displayName, StringComparison.Ordinal)
                    && !foundName.StartsWith(
                        displayName + " (",
                        StringComparison.Ordinal)))
            {
                continue;
            }

            var id = cursor.GetLong(idColumn);
            var ownerPackage = cursor.GetString(ownerPackageIndex);
            matches.Add((
                ContentUris.WithAppendedId(collection, id),
                foundName,
                string.Equals(ownerPackage, packageName, StringComparison.Ordinal)));
        } while (cursor.MoveToNext());

        return matches;
    }

    static void DeleteBestEffort(
        ContentResolver resolver,
        global::Android.Net.Uri uri)
    {
        try
        {
            resolver.Delete(uri, null, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
    }
}
