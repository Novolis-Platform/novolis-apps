namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal sealed class ContentCatalog
{
    public IReadOnlyList<SeriesInfo> Load(string contentRoot)
    {
        var root = Path.GetFullPath(contentRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Content root not found: {root}");

        var seriesList = new List<SeriesInfo>();
        var seriesDir = Path.Combine(root, "content", "series");
        if (Directory.Exists(seriesDir))
        {
            foreach (var dir in Directory.GetDirectories(seriesDir).OrderBy(Path.GetFileName, StringComparer.Ordinal))
                seriesList.Add(LoadSeries(dir));
        }

        return seriesList;
    }

    public BookInfo? FindBook(string contentRoot, string? seriesId, string bookId)
    {
        var catalog = Load(contentRoot);
        if (!string.IsNullOrWhiteSpace(seriesId))
        {
            var series = catalog.FirstOrDefault(s => s.Id.Equals(seriesId, StringComparison.OrdinalIgnoreCase));
            return series?.Books.FirstOrDefault(b => b.Id.Equals(bookId, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var s in catalog)
        {
            var book = s.Books.FirstOrDefault(b => b.Id.Equals(bookId, StringComparison.OrdinalIgnoreCase));
            if (book is not null)
                return book;
        }

        var standaloneDir = Path.Combine(contentRoot, "content", "books", bookId);
        if (Directory.Exists(standaloneDir))
            return LoadBook(standaloneDir, null);

        return null;
    }

    private static SeriesInfo LoadSeries(string seriesDirectory)
    {
        var yaml = YamlUtil.LoadYamlFile(Path.Combine(seriesDirectory, "series.yaml"));
        var id = YamlUtil.GetString(yaml, "id") ?? Path.GetFileName(seriesDirectory);
        var title = YamlUtil.GetString(yaml, "name") ?? id;

        var books = new List<BookInfo>();
        var booksDir = Path.Combine(seriesDirectory, "books");
        if (Directory.Exists(booksDir))
        {
            foreach (var bookDir in Directory.GetDirectories(booksDir).OrderBy(Path.GetFileName, StringComparer.Ordinal))
                books.Add(LoadBook(bookDir, id));
        }

        return new SeriesInfo(id, title, seriesDirectory, books);
    }

    private static BookInfo LoadBook(string bookDirectory, string? seriesId)
    {
        var bookYaml = YamlUtil.LoadYamlFile(Path.Combine(bookDirectory, "book.yaml"));
        var id = Path.GetFileName(bookDirectory);
        var title = YamlUtil.GetString(bookYaml, "title") ?? id;
        var subtitle = YamlUtil.GetString(bookYaml, "subtitle");
        var author = YamlUtil.GetString(bookYaml, "author");
        var orderFromHeading = YamlUtil.GetBool(bookYaml, "chapter_order_from_heading");
        var debugMode = YamlUtil.GetBool(bookYaml, "debug_mode");

        var chapters = new List<ChapterInfo>();
        var chDir = Path.Combine(bookDirectory, "chapters");
        if (Directory.Exists(chDir))
        {
            foreach (var file in Directory.GetFiles(chDir, "*.md").OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                chapters.Add(new ChapterInfo(
                    stem,
                    ChapterOrder.ReadChapterTitle(file) ?? stem,
                    ChapterKind.Chapter,
                    ChapterOrder.GetSortKey(file),
                    file));
            }
        }

        var apDir = Path.Combine(bookDirectory, "appendices");
        if (Directory.Exists(apDir))
        {
            var appendixFiles = Directory.GetFiles(apDir, "*.md").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList();
            for (var i = 0; i < appendixFiles.Count; i++)
            {
                var file = appendixFiles[i];
                var stem = Path.GetFileNameWithoutExtension(file);
                chapters.Add(new ChapterInfo(
                    stem,
                    ChapterOrder.ReadChapterTitle(file) ?? stem,
                    ChapterKind.Appendix,
                    i,
                    file));
            }
        }

        var ordered = ChapterOrder.SortChapters(chapters, orderFromHeading);
        return new BookInfo(id, title, subtitle, author, bookDirectory, seriesId, ordered, orderFromHeading, debugMode);
    }
}
