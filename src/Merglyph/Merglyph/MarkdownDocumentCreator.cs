using Merglyph.Core;

namespace Merglyph;

/// <summary>Creates a new Markdown file under the user's documents folder.</summary>
public sealed class MarkdownDocumentCreator
{
    /// <summary>
    /// Writes a new Untitled Markdown document and returns an open request for it.
    /// Defaults to My Documents (or Personal); falls back to app documents storage
    /// when the preferred folder is not writable.
    /// </summary>
    public async ValueTask<DocumentOpenRequest> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var preferred = UserDocumentsPaths.Resolve();
        var fallback = Path.Combine(FileSystem.AppDataDirectory, "Documents");
        var directory = UserDocumentsPaths.EnsureWritable(preferred, fallback);
        var path = NewMarkdownDocument.CreateUniquePath(directory);

        await File.WriteAllTextAsync(
            path,
            NewMarkdownDocument.DefaultContent,
            cancellationToken);

        var name = new DocumentName(Path.GetFileName(path));
        return new DocumentOpenRequest(
            name,
            async openCancellation =>
            {
                openCancellation.ThrowIfCancellationRequested();
                return File.OpenRead(path);
            },
            directory);
    }
}
