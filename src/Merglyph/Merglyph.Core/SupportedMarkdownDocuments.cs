namespace Merglyph.Core;

public static class SupportedMarkdownDocuments
{
    public static IReadOnlyList<string> Extensions { get; } = [".md", ".markdown", ".mdown", ".mkd"];

    public static bool IsSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
