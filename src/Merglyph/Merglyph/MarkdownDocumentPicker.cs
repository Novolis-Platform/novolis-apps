using Merglyph.Core;

namespace Merglyph;

public sealed class MarkdownDocumentPicker
{
    private static readonly FilePickerFileType MarkdownTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["text/markdown", "text/x-markdown", "text/plain"],
            [DevicePlatform.WinUI] = [".md", ".markdown", ".mdown", ".mkd"],
        });

    public async ValueTask<DocumentOpenRequest?> PickAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Open Markdown document",
            FileTypes = MarkdownTypes,
        });

        if (file is null)
            return null;

        if (!SupportedMarkdownDocuments.IsSupported(file.FileName))
            throw new InvalidDataException("Merglyph opens .md, .markdown, .mdown, and .mkd files only.");

        var directory = string.IsNullOrWhiteSpace(file.FullPath)
            ? null
            : Path.GetDirectoryName(file.FullPath);
        var displayPath = string.IsNullOrWhiteSpace(file.FullPath) ? null : file.FullPath;
        SaveDocumentContent? save = null;
        if (!string.IsNullOrWhiteSpace(file.FullPath))
        {
            var path = file.FullPath;
            save = (content, cancellationToken) =>
                MarkdownDocumentWriter.WriteFileAsync(path, content, cancellationToken);
        }

        return new DocumentOpenRequest(
            new DocumentName(file.FileName),
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await file.OpenReadAsync();
            },
            directory,
            displayPath,
            save);
    }
}
