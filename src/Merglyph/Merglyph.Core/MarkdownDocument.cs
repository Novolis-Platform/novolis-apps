namespace Merglyph.Core;

public sealed record MarkdownDocument(DocumentName Name, MarkdownText Content, string? SourceDirectory = null);
