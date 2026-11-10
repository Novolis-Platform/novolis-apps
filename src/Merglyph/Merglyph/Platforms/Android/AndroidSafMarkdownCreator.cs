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
    /// Opens the system create-document UI and writes the default Markdown body.
    /// Returns null when the user cancels.
    /// </summary>
    public static async Task<DocumentOpenRequest?> CreateAsync(
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
        await using (var output = resolver.OpenOutputStream(uri)
            ?? throw new IOException("Android did not provide a writable document stream."))
        await using (var writer = new StreamWriter(output))
        {
            await writer.WriteAsync(NewMarkdownDocument.DefaultContent.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

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
            DisplayPath: displayPath);
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
