namespace ManuscriptStudio.Extensions.GenericMarkdown;

internal sealed record MarkdownTreeNode(string Name, string? FilePath, IReadOnlyList<MarkdownTreeNode> Children)
{
    public bool IsFile => FilePath is not null;
}
