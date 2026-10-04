using Merglyph.Core;

namespace Merglyph;

public delegate ValueTask<Stream> OpenDocumentStream(CancellationToken cancellationToken);

public sealed record DocumentOpenRequest(
    DocumentName Name,
    OpenDocumentStream OpenReadAsync,
    string? SourceDirectory = null);

public sealed class DocumentSession(MarkdownDocumentReader reader)
{
    public MarkdownDocument? Current { get; private set; }

    public async ValueTask<MarkdownDocument> OpenAsync(
        DocumentOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await request.OpenReadAsync(cancellationToken);
        var document = await reader.ReadAsync(request.Name, stream, cancellationToken);
        Current = document with { SourceDirectory = request.SourceDirectory };
        return Current;
    }
}
