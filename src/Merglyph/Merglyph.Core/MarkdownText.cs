namespace Merglyph.Core;

public readonly record struct MarkdownText
{
    public MarkdownText(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
    public string Value { get; }
}
