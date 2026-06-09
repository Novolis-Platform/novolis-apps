using ManuscriptStudio.Extensions.BookAuthoring.Content;

namespace ManuscriptStudio.Extensions.BookAuthoring.Navigation;

internal static class BookNavigationTreeBuilder
{
    public static IReadOnlyList<BookNavigationNode> Build(
        SeriesInfo? series,
        BookInfo? book,
        BookNavigationScope scope)
    {
        var nodes = new List<BookNavigationNode>();

        if (scope is BookNavigationScope.All or BookNavigationScope.References)
            AddReferenceSets(nodes, "Series references", series?.References ?? []);

        if (book is null)
            return nodes;

        if (scope is BookNavigationScope.All or BookNavigationScope.Chapters)
        {
            var chapters = book.Chapters.Where(c => c.Kind == ChapterKind.Chapter).ToList();
            if (chapters.Count > 0)
            {
                nodes.Add(new BookNavigationNode(
                    "Chapters",
                    null,
                    chapters.Select(c => new BookNavigationNode(
                        FormatChapterLabel(c),
                        c.FilePath,
                        [])).ToList()));
            }

            var appendices = book.Chapters.Where(c => c.Kind == ChapterKind.Appendix).ToList();
            if (appendices.Count > 0)
            {
                nodes.Add(new BookNavigationNode(
                    "Appendices",
                    null,
                    appendices.Select(c => new BookNavigationNode(
                        FormatChapterLabel(c),
                        c.FilePath,
                        [])).ToList()));
            }
        }

        if (scope is BookNavigationScope.All or BookNavigationScope.References)
            AddReferenceSets(nodes, "Book references", book.References);

        return nodes;
    }

    private static void AddReferenceSets(
        List<BookNavigationNode> nodes,
        string groupLabel,
        IReadOnlyList<ReferenceSetInfo> sets)
    {
        if (sets.Count == 0)
            return;

        nodes.Add(new BookNavigationNode(
            groupLabel,
            null,
            sets.Select(set => new BookNavigationNode(
                set.Title,
                null,
                set.Files.Select(f => new BookNavigationNode(f.Title, f.FilePath, [])).ToList())).ToList()));
    }

    private static string FormatChapterLabel(ChapterInfo chapter) =>
        chapter.Kind == ChapterKind.Appendix
            ? $"{chapter.Id} — {chapter.Title}"
            : $"{chapter.SortKey:0.###} — {chapter.Title}";
}
