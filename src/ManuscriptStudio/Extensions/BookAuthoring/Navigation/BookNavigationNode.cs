namespace ManuscriptStudio.Extensions.BookAuthoring.Navigation;

internal sealed record BookNavigationNode(
    string Label,
    string? FilePath,
    IReadOnlyList<BookNavigationNode> Children)
{
    public bool IsFile => FilePath is not null;
}
