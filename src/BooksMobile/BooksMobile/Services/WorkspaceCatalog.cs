using Novolis.Markup.Manuscript;

namespace BooksMobile.Services;

/// <summary>
/// Loads a browseable book catalog from either Manuscript <c>content/</c> layout
/// or a MkDocs-style <c>docs/</c> tree (articles / dossiers / series).
/// </summary>
public static class WorkspaceCatalog
{
    public static IReadOnlyList<SeriesInfo> LoadSeries(string workspaceRoot)
    {
        if (ManuscriptWorkspace.TryOpen(workspaceRoot, out var ws) && ws is not null)
            return ws.Catalog.Load(ws.ContentRoot);
        return [];
    }

    public static IReadOnlyList<BookInfo> LoadBooks(string workspaceRoot)
    {
        if (ManuscriptWorkspace.TryOpen(workspaceRoot, out var ws) && ws is not null)
            return ws.Catalog.LoadStandaloneBooks(ws.ContentRoot);

        var docs = Path.Combine(workspaceRoot, "docs");
        if (!Directory.Exists(docs))
            docs = workspaceRoot;

        var books = new List<BookInfo>();
        AddFolderBook(books, docs, "articles", "Selections", "Republished selections");
        AddFolderBook(books, docs, "dossiers", "Dossiers", "Curated reading packets");
        AddFolderBook(books, docs, "series", "Series", "Continuing reader tracks");

        // Top-level editorial pages as their own short book.
        var rootPages = Directory.Exists(docs)
            ? Directory.GetFiles(docs, "*.md", SearchOption.TopDirectoryOnly)
            : [];
        if (rootPages.Length > 0)
        {
            books.Add(MakeBook(
                "editorial",
                "Editorial",
                "Front matter and house pages",
                docs,
                rootPages));
        }

        return books;
    }

    static void AddFolderBook(
        List<BookInfo> books,
        string docsRoot,
        string folderName,
        string title,
        string subtitle)
    {
        var dir = Path.Combine(docsRoot, folderName);
        if (!Directory.Exists(dir))
            return;
        var files = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
            .Where(f => !string.Equals(Path.GetFileName(f), "index.md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
            return;
        books.Add(MakeBook(folderName, title, subtitle, dir, files));
    }

    static BookInfo MakeBook(
        string id,
        string title,
        string? subtitle,
        string directoryPath,
        IReadOnlyList<string> files)
    {
        var chapters = new List<ChapterInfo>();
        var sort = 0.0;
        foreach (var file in files)
        {
            sort += 1;
            var name = Path.GetFileNameWithoutExtension(file);
            chapters.Add(new ChapterInfo(
                Id: name,
                Title: Humanize(name),
                Kind: ChapterKind.Chapter,
                SortKey: sort,
                FilePath: file));
        }

        return new BookInfo(
            Id: id,
            Title: title,
            Subtitle: subtitle,
            Author: null,
            DirectoryPath: directoryPath,
            SeriesId: null,
            Chapters: chapters,
            ChapterOrderFromHeading: false,
            DebugMode: false,
            References: []);
    }

    static string Humanize(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return slug;
        var parts = slug.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0)
                continue;
            parts[i] = char.ToUpperInvariant(p[0]) + p[1..];
        }

        return string.Join(' ', parts);
    }
}
