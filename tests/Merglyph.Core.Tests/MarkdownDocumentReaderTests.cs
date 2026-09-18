using System.Text;

namespace Merglyph.Core.Tests;

public sealed class MarkdownDocumentReaderTests
{
    [Test]
    public async Task ReadAsync_ReadsUtf8Markdown(CancellationToken cancellationToken)
    {
        var source = new MemoryStream(Encoding.UTF8.GetBytes("# Hello\n\nWorld"));
        var reader = new MarkdownDocumentReader();

        var document = await reader.ReadAsync(
            new DocumentName("hello.md"),
            source,
            cancellationToken);

        await Assert.That(document.Name.Value).IsEqualTo("hello.md");
        await Assert.That(document.Content.Value).IsEqualTo("# Hello\n\nWorld");
    }

    [Test]
    public async Task ReadAsync_RejectsOversizedInput(CancellationToken cancellationToken)
    {
        var source = new MemoryStream(new byte[MarkdownDocumentReader.MaximumDocumentBytes + 1]);
        var reader = new MarkdownDocumentReader();

        Func<Task> act = async () =>
            await reader.ReadAsync(new DocumentName("large.md"), source, cancellationToken);

        await Assert.That(act).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("readme.md", true)]
    [Arguments("README.MARKDOWN", true)]
    [Arguments("diagram.mdown", true)]
    [Arguments("notes.MKD", true)]
    [Arguments("notes.txt", false)]
    public async Task IsSupported_RecognizesOnlyMarkdownExtensions(string fileName, bool expected) =>
        await Assert.That(SupportedMarkdownDocuments.IsSupported(fileName)).IsEqualTo(expected);
}
