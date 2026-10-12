using Android.App;
using Android.Content;
using Android.Provider;
using PresenceLedger.App;
using PresenceLedger.Core;

namespace PresenceLedger.Android;

/// <summary>Imports legacy ledger files through Android's user-authorized folder picker.</summary>
public sealed class AndroidLegacyLedgerImport : ILedgerStorageRecovery
{
    /// <summary>Request code consumed by <see cref="MainActivity"/>.</summary>
    public const int RequestCode = 7403;

    readonly Context _context;
    readonly string _destinationRoot;
    readonly IPresenceDiagnosticsStore _diagnostics;
    readonly LedgerStorageStatus _storageStatus;
    readonly Func<Activity?> _activityProvider;
    readonly object _gate = new();
    TaskCompletionSource<int>? _pending;

    /// <summary>Creates a legacy ledger importer.</summary>
    public AndroidLegacyLedgerImport(
        Context context,
        string destinationRoot,
        IPresenceDiagnosticsStore diagnostics,
        LedgerStorageStatus storageStatus,
        Func<Activity?> activityProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        _destinationRoot = destinationRoot;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _storageStatus = storageStatus ?? throw new ArgumentNullException(nameof(storageStatus));
        _activityProvider = activityProvider ?? throw new ArgumentNullException(nameof(activityProvider));
    }

    /// <inheritdoc />
    public Task<int> RequestImportAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activity = _activityProvider()
            ?? throw new InvalidOperationException("Presence Ledger activity is unavailable.");
        var completion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pending is not null)
                throw new InvalidOperationException("A ledger folder import is already open.");
            _pending = completion;
        }

        using var intent = new Intent(Intent.ActionOpenDocumentTree)
            .AddFlags(
                ActivityFlags.GrantReadUriPermission
                | ActivityFlags.GrantWriteUriPermission
                | ActivityFlags.GrantPersistableUriPermission);
        try
        {
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch (Exception exception)
        {
            ClearPending(completion);
            completion.TrySetException(exception);
        }

        return AwaitAsync(completion, cancellationToken);
    }

    /// <summary>Completes the folder-picker request from the host activity.</summary>
    public void Complete(global::Android.App.Result resultCode, Intent? data)
    {
        TaskCompletionSource<int>? completion;
        lock (_gate)
        {
            completion = _pending;
            _pending = null;
        }

        if (completion is null)
            return;

        _ = CompleteAsync(completion, resultCode, data);
    }

    async Task CompleteAsync(
        TaskCompletionSource<int> completion,
        global::Android.App.Result resultCode,
        Intent? data)
    {
        if (resultCode != global::Android.App.Result.Ok
            || data?.Data is not { } treeUri)
        {
            completion.TrySetResult(0);
            return;
        }

        try
        {
            PersistPermission(data, treeUri);
            var copied = CopyTree(treeUri);
            _storageStatus.RecordImport(copied, errorMessage: null);
            await RecordAsync(
                "info",
                "legacy-import-completed",
                $"Imported {copied} legacy ledger file(s) from the selected folder.",
                treeUri.ToString());
            completion.TrySetResult(copied);
        }
        catch (Exception exception)
        {
            _storageStatus.RecordImport(0, exception.Message);
            await RecordAsync(
                "error",
                "legacy-import-failed",
                "Could not import the selected legacy ledger folder.",
                exception.ToString());
            completion.TrySetException(exception);
        }
    }

    int CopyTree(global::Android.Net.Uri treeUri)
    {
        var resolver = _context.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver is unavailable.");
        var rootDocumentId = DocumentsContract.GetTreeDocumentId(treeUri)
            ?? throw new InvalidOperationException("Android did not provide a tree document id.");
        return CopyChildren(resolver, treeUri, rootDocumentId, string.Empty);
    }

    int CopyChildren(
        ContentResolver resolver,
        global::Android.Net.Uri treeUri,
        string parentDocumentId,
        string relativeDirectory)
    {
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(
                treeUri,
                parentDocumentId)
            ?? throw new InvalidOperationException("Android did not provide a child document URI.");
        using var cursor = resolver.Query(
            childrenUri,
            [
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnDisplayName,
                DocumentsContract.Document.ColumnMimeType,
            ],
            null,
            null,
            null);
        if (cursor is null)
            return 0;

        var idColumn = cursor.GetColumnIndexOrThrow(
            DocumentsContract.Document.ColumnDocumentId);
        var nameColumn = cursor.GetColumnIndexOrThrow(
            DocumentsContract.Document.ColumnDisplayName);
        var mimeColumn = cursor.GetColumnIndexOrThrow(
            DocumentsContract.Document.ColumnMimeType);
        var copied = 0;
        while (cursor.MoveToNext())
        {
            var documentId = cursor.GetString(idColumn);
            var name = cursor.GetString(nameColumn);
            var mimeType = cursor.GetString(mimeColumn);
            if (string.IsNullOrWhiteSpace(documentId)
                || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.Equals(
                    mimeType,
                    DocumentsContract.Document.MimeTypeDir,
                    StringComparison.Ordinal))
            {
                var childDirectory = CombineRelative(relativeDirectory, name);
                copied += CopyChildren(
                    resolver,
                    treeUri,
                    documentId,
                    childDirectory);
                continue;
            }

            if (!IsLedgerPayload(name))
                continue;

            var relativeFile = CombineRelative(relativeDirectory, name);
            var documentUri = DocumentsContract.BuildDocumentUriUsingTree(
                    treeUri,
                    documentId)
                ?? throw new InvalidOperationException($"Android did not provide {relativeFile}.");
            if (CopyFile(resolver, documentUri, relativeFile))
                copied++;
        }

        return copied;
    }

    bool CopyFile(
        ContentResolver resolver,
        global::Android.Net.Uri source,
        string relativeFile)
    {
        var target = Path.GetFullPath(Path.Combine(
            _destinationRoot,
            relativeFile.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_destinationRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        if (File.Exists(target) && new FileInfo(target).Length > 0)
            return false;

        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        using var input = resolver.OpenInputStream(source)
            ?? throw new InvalidOperationException($"Android could not open {relativeFile}.");
        using var output = new FileStream(
            target,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.SequentialScan,
            });
        input.CopyTo(output);
        return true;
    }

    static bool IsLedgerPayload(string name) =>
        name.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "presence-report.txt", StringComparison.OrdinalIgnoreCase);

    static string CombineRelative(string directory, string name) =>
        string.IsNullOrEmpty(directory)
            ? name
            : $"{directory}/{name}";

    void PersistPermission(Intent data, global::Android.Net.Uri treeUri)
    {
        var persistable = data.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if (persistable == 0)
            return;

        try
        {
            _context.ContentResolver?.TakePersistableUriPermission(treeUri, persistable);
        }
        catch (global::Java.Lang.SecurityException)
        {
            // Some document providers grant only a one-session permission.
        }
    }

    async Task RecordAsync(
        string level,
        string code,
        string message,
        string? detail)
    {
        try
        {
            await _diagnostics.AppendAsync(
                new PresenceDiagnosticRecord(
                    DateTimeOffset.UtcNow,
                    level,
                    code,
                    message,
                    detail));
        }
        catch
        {
            // Import results are still returned when diagnostics cannot be written.
        }
    }

    void ClearPending(TaskCompletionSource<int> completion)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pending, completion))
                _pending = null;
        }
    }

    async Task<int> AwaitAsync(
        TaskCompletionSource<int> completion,
        CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(
            () =>
            {
                ClearPending(completion);
                completion.TrySetCanceled(cancellationToken);
            });
        return await completion.Task.ConfigureAwait(false);
    }
}
