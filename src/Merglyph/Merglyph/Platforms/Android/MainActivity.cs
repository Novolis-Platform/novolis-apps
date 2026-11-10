using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Merglyph.Core;
using Novolis.Maui.Activation;

namespace Merglyph;

[Register("dev.novolis.merglyph.MainActivity")]
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/markdown")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/x-markdown")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/plain")]
public sealed class MainActivity : MauiAppCompatActivity
{
    private const int CreateMarkdownDocumentRequest = 0x4D47; // MG
    private const int OpenMarkdownDocumentRequest = 0x4D48; // MH
    private TaskCompletionSource<Android.Net.Uri?>? _documentPickerCompletion;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Publish(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        Publish(intent);
    }

    /// <summary>Opens SAF create-document and returns the chosen content URI, or null if cancelled.</summary>
    internal Task<Android.Net.Uri?> CreateMarkdownDocumentAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("text/markdown");
        intent.PutExtra(Intent.ExtraTitle, suggestedFileName);
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "text/markdown", "text/plain" });
        intent.AddFlags(
            ActivityFlags.GrantReadUriPermission
            | ActivityFlags.GrantWriteUriPermission
            | ActivityFlags.GrantPersistableUriPermission);
        return StartDocumentPickerAsync(intent, CreateMarkdownDocumentRequest, cancellationToken);
    }

    /// <summary>Opens SAF open-document and returns the chosen content URI, or null if cancelled.</summary>
    internal Task<Android.Net.Uri?> OpenMarkdownDocumentAsync(
        CancellationToken cancellationToken = default)
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(
            Intent.ExtraMimeTypes,
            new[] { "text/markdown", "text/x-markdown", "text/plain", "application/octet-stream" });
        intent.AddFlags(
            ActivityFlags.GrantReadUriPermission
            | ActivityFlags.GrantWriteUriPermission
            | ActivityFlags.GrantPersistableUriPermission);
        return StartDocumentPickerAsync(intent, OpenMarkdownDocumentRequest, cancellationToken);
    }

    private Task<Android.Net.Uri?> StartDocumentPickerAsync(
        Intent intent,
        int requestCode,
        CancellationToken cancellationToken)
    {
        if (_documentPickerCompletion is not null)
            throw new InvalidOperationException("A document picker is already open.");

        var completion = new TaskCompletionSource<Android.Net.Uri?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _documentPickerCompletion = completion;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (_documentPickerCompletion == completion)
                    _documentPickerCompletion = null;
                completion.TrySetCanceled(cancellationToken);
            });
        }

        StartActivityForResult(intent, requestCode);
        return completion.Task;
    }

    protected override void OnActivityResult(
        int requestCode,
        Result resultCode,
        Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode is not (CreateMarkdownDocumentRequest or OpenMarkdownDocumentRequest))
            return;

        var completion = _documentPickerCompletion;
        _documentPickerCompletion = null;
        if (completion is null)
            return;

        if (resultCode != Result.Ok || data?.Data is not { } uri)
        {
            completion.TrySetResult(null);
            return;
        }

        TryPersistWritePermission(data, uri);
        completion.TrySetResult(uri);
    }

    private void TryPersistWritePermission(Intent intent, Android.Net.Uri uri)
    {
        var flags = intent.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if (flags == 0)
        {
            flags = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;
        }

        if (ContentResolver is null)
            return;

        try
        {
            ContentResolver.TakePersistableUriPermission(uri, flags);
        }
        catch (Java.Lang.SecurityException)
        {
        }
    }

    private void Publish(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView || intent.Data is not { } uri)
            return;

        TryPersistReadPermission(intent, uri);
        var name = ResolveName(uri);
        if (!SupportedMarkdownDocuments.IsSupported(name))
            return;

        var filePath = string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase)
            ? uri.Path
            : null;
        SaveDocumentContent? save = null;
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            var path = filePath;
            save = (content, cancellationToken) =>
                MarkdownDocumentWriter.WriteFileAsync(path, content, cancellationToken);
        }
        else if (ContentResolver is not null)
        {
            var resolver = ContentResolver;
            save = (content, cancellationToken) =>
                WriteContentUriAsync(resolver, uri, content, cancellationToken);
        }

        MauiActivationBridge<DocumentOpenRequest>.Publish(new DocumentOpenRequest(
            new DocumentName(name),
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    return ValueTask.FromResult<Stream>(File.OpenRead(filePath));

                var stream = ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Android did not provide a readable document stream.");
                return ValueTask.FromResult<Stream>(stream);
            },
            SourceDirectory: string.IsNullOrWhiteSpace(filePath)
                ? null
                : Path.GetDirectoryName(filePath),
            DisplayPath: name,
            SaveAsync: save));
    }

    private static async ValueTask WriteContentUriAsync(
        ContentResolver resolver,
        Android.Net.Uri uri,
        string content,
        CancellationToken cancellationToken)
    {
        await using var output = resolver.OpenOutputStream(uri, "wt")
            ?? throw new IOException("Android did not provide a writable document stream.");
        await MarkdownDocumentWriter.WriteStreamAsync(output, content, cancellationToken)
            .ConfigureAwait(false);
    }

    private void TryPersistReadPermission(Intent intent, Android.Net.Uri uri)
    {
        var persistableFlags = intent.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if ((intent.Flags & ActivityFlags.GrantPersistableUriPermission) == 0
            || persistableFlags == 0
            || ContentResolver is null)
        {
            return;
        }

        try
        {
            ContentResolver.TakePersistableUriPermission(uri, persistableFlags);
        }
        catch (Java.Lang.SecurityException)
        {
        }
    }

    private string ResolveName(Android.Net.Uri uri)
    {
        using ICursor? cursor = ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
        if (cursor?.MoveToFirst() is true)
        {
            var column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (column >= 0 && cursor.GetString(column) is { Length: > 0 } displayName)
                return displayName;
        }

        var fallback = Android.Net.Uri.Decode(uri.LastPathSegment ?? string.Empty);
        return string.IsNullOrWhiteSpace(fallback) ? "document.md" : fallback;
    }
}
