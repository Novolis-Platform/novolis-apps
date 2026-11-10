using Merglyph.Core;

namespace Merglyph;

/// <summary>
/// Creates a new Markdown file in a user-visible location.
/// Windows uses My Documents; Android uses the system create-document (SAF) picker.
/// </summary>
public sealed class MarkdownDocumentCreator
{
    /// <summary>
    /// Writes a new Untitled Markdown document and returns an open request for it.
    /// On Android the user picks the visible Documents/Downloads target via SAF.
    /// </summary>
    public ValueTask<DocumentOpenRequest> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(NewMarkdownDocument.DefaultContent, cancellationToken);

    /// <summary>
    /// Writes <paramref name="content"/> to a new user-visible Markdown file
    /// (used for Create and Save As).
    /// </summary>
    public async ValueTask<DocumentOpenRequest> CreateAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        content ??= string.Empty;

#if ANDROID
        var saf = await AndroidSafMarkdownCreator.CreateAsync(content, cancellationToken)
            .ConfigureAwait(false);
        if (saf is null)
            throw new OperationCanceledException("Document create was cancelled.");
        return saf;
#else
        var preferred = UserDocumentsPaths.Resolve();
        var fallback = Path.Combine(FileSystem.AppDataDirectory, "Documents");
        var directory = UserDocumentsPaths.EnsureWritable(preferred, fallback);
        var path = NewMarkdownDocument.CreateUniquePath(directory);

        await MarkdownDocumentWriter.WriteFileAsync(path, content, cancellationToken)
            .ConfigureAwait(false);

        var name = new DocumentName(Path.GetFileName(path));
        return new DocumentOpenRequest(
            name,
            async openCancellation =>
            {
                openCancellation.ThrowIfCancellationRequested();
                return File.OpenRead(path);
            },
            directory,
            path,
            (body, saveCancellation) =>
                MarkdownDocumentWriter.WriteFileAsync(path, body, saveCancellation));
#endif
    }
}
