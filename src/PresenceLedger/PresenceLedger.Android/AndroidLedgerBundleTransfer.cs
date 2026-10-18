using Android.App;
using Android.Content;
using Android.Provider;
using PresenceLedger.App;

namespace PresenceLedger.Android;

/// <summary>Connects the full ledger backup actions to Android's document picker.</summary>
public sealed class AndroidLedgerBundleTransfer : ILedgerBundleTransfer
{
    public const int ImportRequestCode = 7410;
    public const int ExportRequestCode = 7411;

    readonly Context _context;
    readonly Func<Activity?> _activityProvider;
    readonly object _gate = new();
    TaskCompletionSource<Stream?>? _pending;
    bool _pendingWrite;

    /// <summary>Creates a document-picker bridge for ledger archives.</summary>
    public AndroidLedgerBundleTransfer(
        Context context,
        Func<Activity?> activityProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _activityProvider = activityProvider ?? throw new ArgumentNullException(nameof(activityProvider));
    }

    /// <inheritdoc />
    public Task<Stream?> OpenImportAsync(CancellationToken cancellationToken = default)
    {
        var intent = new Intent(Intent.ActionOpenDocument)
            .SetType("application/zip")
            .AddCategory(Intent.CategoryOpenable)
            .AddFlags(
                ActivityFlags.GrantReadUriPermission
                | ActivityFlags.GrantPersistableUriPermission);
        return RequestAsync(intent, ImportRequestCode, write: false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream?> CreateExportAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        var intent = new Intent(Intent.ActionCreateDocument)
            .SetType("application/zip")
            .PutExtra(Intent.ExtraTitle, suggestedFileName)
            .AddCategory(Intent.CategoryOpenable)
            .AddFlags(
                ActivityFlags.GrantWriteUriPermission
                | ActivityFlags.GrantReadUriPermission);
        return RequestAsync(intent, ExportRequestCode, write: true, cancellationToken);
    }

    /// <summary>Completes a pending Android document-picker request.</summary>
    public void Complete(int requestCode, Result resultCode, Intent? data)
    {
        TaskCompletionSource<Stream?>? completion;
        bool write;
        lock (_gate)
        {
            if (_pending is null
                || (requestCode != ImportRequestCode && requestCode != ExportRequestCode))
            {
                return;
            }

            completion = _pending;
            write = _pendingWrite;
            _pending = null;
            _pendingWrite = false;
        }

        if (resultCode != Result.Ok || data?.Data is not { } uri)
        {
            completion.TrySetResult(null);
            return;
        }

        try
        {
            PersistPermission(data, uri);
            var stream = write
                ? _context.ContentResolver?.OpenOutputStream(uri)
                : _context.ContentResolver?.OpenInputStream(uri);
            if (stream is null)
            {
                throw new IOException("Android did not provide a usable ledger archive stream.");
            }

            completion.TrySetResult(stream);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    Task<Stream?> RequestAsync(
        Intent intent,
        int requestCode,
        bool write,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activity = _activityProvider()
            ?? throw new InvalidOperationException("Presence Ledger activity is unavailable.");
        var completion = new TaskCompletionSource<Stream?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pending is not null)
                throw new InvalidOperationException("A ledger archive picker is already open.");
            _pending = completion;
            _pendingWrite = write;
        }

        try
        {
            activity.StartActivityForResult(intent, requestCode);
        }
        catch (Exception exception)
        {
            ClearPending(completion);
            completion.TrySetException(exception);
        }

        return AwaitAsync(completion, cancellationToken);
    }

    void PersistPermission(Intent data, global::Android.Net.Uri uri)
    {
        var flags = data.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if ((data.Flags & ActivityFlags.GrantPersistableUriPermission) == 0
            || flags == 0
            || _context.ContentResolver is null)
        {
            return;
        }

        try
        {
            _context.ContentResolver.TakePersistableUriPermission(uri, flags);
        }
        catch (global::Java.Lang.SecurityException)
        {
        }
    }

    void ClearPending(TaskCompletionSource<Stream?> completion)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pending, completion))
            {
                _pending = null;
                _pendingWrite = false;
            }
        }
    }

    async Task<Stream?> AwaitAsync(
        TaskCompletionSource<Stream?> completion,
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
