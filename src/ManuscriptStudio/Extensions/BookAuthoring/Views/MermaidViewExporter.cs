using System.Text.Json;
using ManuscriptStudio.Extensions.BookAuthoring.Content;

namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal sealed class MermaidViewExporter
{
    private readonly BookMetadataIndex _index = new();

    public string ExportDirectory(string dataRoot, BookInfo book) =>
        Path.Combine(
            dataRoot,
            "exports",
            book.SeriesId ?? "standalone",
            book.Id,
            "views");

    public void ExportView(string dataRoot, BookInfo book, string viewId, string mermaid)
    {
        var dir = ExportDirectory(dataRoot, book);
        Directory.CreateDirectory(dir);
        var fileName = ViewFileName(viewId);
        File.WriteAllText(Path.Combine(dir, fileName), mermaid);
    }

    public void ExportAll(string dataRoot, BookInfo book, string timeline, string relationships, string map)
    {
        var dir = ExportDirectory(dataRoot, book);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "timeline.mmd"), timeline);
        File.WriteAllText(Path.Combine(dir, "relationships.mmd"), relationships);
        File.WriteAllText(Path.Combine(dir, "map.mmd"), map);

        var manifest = new
        {
            generatedAt = DateTime.UtcNow,
            seriesId = book.SeriesId,
            bookId = book.Id,
            bookTitle = book.Title,
            chapterCount = book.Chapters.Count,
            views = new[] { "timeline", "relationships", "map" },
        };
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json);
    }

    public (string Timeline, string Relationships, string Map) BuildAll(BookInfo book)
    {
        var entries = _index.Build(book);
        return (
            TimelineMermaidBuilder.Build(book, entries),
            RelationshipMermaidBuilder.Build(entries),
            PlacesMermaidBuilder.Build(entries));
    }

    public static string ViewFileName(string viewId) =>
        viewId switch
        {
            BookViewIds.Timeline => "timeline.mmd",
            BookViewIds.Relationships => "relationships.mmd",
            BookViewIds.Map => "map.mmd",
            _ => $"{viewId}.mmd",
        };
}

internal static class BookViewIds
{
    public const string Preview = "preview";
    public const string Timeline = "timeline";
    public const string Relationships = "relationships";
    public const string Map = "map";
}
