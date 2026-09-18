namespace Merglyph.Core;

public readonly record struct DocumentName
{
    public DocumentName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Document name must contain a file name.", nameof(value));
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct MarkdownText
{
    public MarkdownText(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
    public string Value { get; }
}

public sealed record MarkdownDocument(DocumentName Name, MarkdownText Content);

public static class SupportedMarkdownDocuments
{
    public static IReadOnlyList<string> Extensions { get; } = [".md", ".markdown", ".mdown", ".mkd"];

    public static bool IsSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
