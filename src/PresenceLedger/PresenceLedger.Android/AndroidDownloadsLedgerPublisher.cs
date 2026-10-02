using Android.Content;
using Android.Provider;
using PresenceLedger.App;
using PresenceLedger.Storage;
using System.Runtime.Versioning;

namespace PresenceLedger.Android;

/// <summary>Publishes the ledger into the public Downloads folder so My Files and a computer can read it.</summary>
public sealed class AndroidDownloadsLedgerPublisher : ILedgerFilePublisher
{
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

        foreach (var file in LedgerFiles.List(_sourceRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(resolver, collection, file);
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

        var existing = Find(resolver, collection, displayName, relativePath);
        if (existing is not null)
        {
            using var stream = resolver.OpenOutputStream(existing, "wt")
                ?? throw new InvalidOperationException($"Android could not open {file.RelativePath}.");
            using var input = File.OpenRead(file.FullPath);
            input.CopyTo(stream);
            return;
        }

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
        values.Put(MediaStore.IMediaColumns.MimeType, "application/x-ndjson");
        values.Put(MediaStore.IMediaColumns.RelativePath, relativePath);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);
        var uri = resolver.Insert(collection, values)
            ?? throw new InvalidOperationException($"Android could not create {file.RelativePath}.");
        try
        {
            using var stream = resolver.OpenOutputStream(uri)
                ?? throw new InvalidOperationException($"Android could not write {file.RelativePath}.");
            using var input = File.OpenRead(file.FullPath);
            input.CopyTo(stream);
        }
        finally
        {
            var ready = new ContentValues();
            ready.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, ready, null, null);
        }
    }

    [SupportedOSPlatform("android29.0")]
    static global::Android.Net.Uri? Find(
        ContentResolver resolver,
        global::Android.Net.Uri collection,
        string displayName,
        string relativePath)
    {
        using var cursor = resolver.Query(
            collection,
            ["_id"],
            $"{MediaStore.IMediaColumns.DisplayName}=? AND {MediaStore.IMediaColumns.RelativePath}=?",
            [displayName, relativePath],
            null);
        if (cursor is null || !cursor.MoveToFirst())
            return null;

        var id = cursor.GetLong(cursor.GetColumnIndexOrThrow("_id"));
        return ContentUris.WithAppendedId(collection, id);
    }
}
