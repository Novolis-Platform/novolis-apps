#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using Merglyph.Core;
using Microsoft.Maui.ApplicationModel;

namespace Merglyph;

/// <summary>Creates a user-visible Markdown file through Android SAF create-document.</summary>
internal static class AndroidSafMarkdownCreator
{
    /// <summary>
    /// Opens the system create-document UI and writes the Markdown body.
    /// Returns null when the user cancels.
    /// </summary>
    public static async Task<DocumentOpenRequest?> CreateAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Platform.CurrentActivity is not MainActivity activity)
            throw new InvalidOperationException(
                "Merglyph needs the Android main activity to create a visible Markdown file.");

        var suggestedName = NewMarkdownDocument.DefaultStem + ".md";
        var uri = await activity.CreateMarkdownDocumentAsync(suggestedName, cancellationToken)
            .ConfigureAwait(false);
        if (uri is null)
            return null;

        var resolver = activity.ContentResolver
            ?? throw new IOException("Android ContentResolver is unavailable.");
        await WriteUriAsync(resolver, uri, content, cancellationToken).ConfigureAwait(false);

        var name = ResolveDisplayName(activity, uri);
        var displayPath = uri.ToString() ?? name;
        return new DocumentOpenRequest(
            new DocumentName(name),
            openCancellation =>
            {
                openCancellation.ThrowIfCancellationRequested();
                var stream = resolver.OpenInputStream(uri)
                    ?? throw new IOException("Android did not provide a readable document stream.");
                return ValueTask.FromResult<Stream>(stream);
            },
            SourceDirectory: null,
            DisplayPath: displayPath,
            SaveAsync: (body, saveCancellation) =>
                WriteUriAsync(resolver, uri, body, saveCancellation));
    }

    private static async ValueTask WriteUriAsync(
        ContentResolver resolver,
        Android.Net.Uri uri,
        string content,
        CancellationToken cancellationToken)
    {
        // "wt" truncates so Save replaces the whole document.
        await using var output = resolver.OpenOutputStream(uri, "wt")
            ?? throw new IOException("Android did not provide a writable document stream.");
        await MarkdownDocumentWriter.WriteStreamAsync(output, content, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveDisplayName(Activity activity, Android.Net.Uri uri)
    {
        using var cursor = activity.ContentResolver?.Query(
            uri,
            [IOpenableColumns.DisplayName],
            null,
            null,
            null);
        if (cursor?.MoveToFirst() is true)
        {
            var column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (column >= 0 && cursor.GetString(column) is { Length: > 0 } displayName)
                return displayName;
        }

        var fallback = Android.Net.Uri.Decode(uri.LastPathSegment ?? string.Empty);
        return string.IsNullOrWhiteSpace(fallback) ? "Untitled.md" : fallback;
    }
}
#endif
