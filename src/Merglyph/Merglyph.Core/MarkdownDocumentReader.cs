using System.Text;

namespace Merglyph.Core;

public sealed class MarkdownDocumentReader
{
    public const int MaximumDocumentBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public async ValueTask<MarkdownDocument> ReadAsync(
        DocumentName name,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek && source.Length > MaximumDocumentBytes)
            throw new InvalidDataException($"Markdown documents are limited to {MaximumDocumentBytes / (1024 * 1024)} MiB.");

        using var buffer = new MemoryStream();
        var chunk = GC.AllocateUninitializedArray<byte>(16 * 1024);
        var total = 0;

        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > MaximumDocumentBytes)
                throw new InvalidDataException($"Markdown documents are limited to {MaximumDocumentBytes / (1024 * 1024)} MiB.");

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        var bytes = buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length));
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];

        return new MarkdownDocument(name, new MarkdownText(Utf8.GetString(bytes)));
    }
}
