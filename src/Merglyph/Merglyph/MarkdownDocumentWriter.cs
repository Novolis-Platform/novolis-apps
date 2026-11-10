using System.Text;

namespace Merglyph;

/// <summary>Shared UTF-8 Markdown write helpers for file and stream targets.</summary>
internal static class MarkdownDocumentWriter
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static async ValueTask WriteFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await File.WriteAllTextAsync(path, content ?? string.Empty, Utf8, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask WriteStreamAsync(
        Stream stream,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.CanSeek)
        {
            stream.SetLength(0);
            stream.Position = 0;
        }

        await using var writer = new StreamWriter(stream, Utf8, leaveOpen: true);
        await writer.WriteAsync((content ?? string.Empty).AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (stream.CanSeek)
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
