using ManuscriptStudio.Extensions.BookAuthoring.Content;
using ManuscriptStudio.Extensions.BookAuthoring.Views;

namespace ManuscriptStudio.Smoke;

internal static class CalypsoViewsSmoke
{
    private const string ContentRoot = @"D:\repos\books";
    private const string SeriesId = "the-calypso-cycle";
    private const string BookId = "calypso";

    public static int Run(string dataRoot)
    {
        if (!Directory.Exists(ContentRoot))
        {
            Console.Error.WriteLine($"Calypso smoke: content root not found: {ContentRoot}");
            return 1;
        }

        var catalog = new ContentCatalog();
        var book = catalog.FindBook(ContentRoot, SeriesId, BookId);
        if (book is null)
        {
            Console.Error.WriteLine($"Calypso smoke: book '{BookId}' not found under series '{SeriesId}'.");
            return 1;
        }

        var exporter = new MermaidViewExporter();
        var (timeline, relationships, map) = exporter.BuildAll(book);

        if (!timeline.Contains("2496.099", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Calypso smoke: timeline missing chapter date 2496.099.");
            return 1;
        }

        if (!relationships.Contains("Marsh", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Calypso smoke: relationships missing character Marsh.");
            return 1;
        }

        if (!map.Contains("Calypso", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Calypso smoke: map missing location Calypso.");
            return 1;
        }

        exporter.ExportAll(dataRoot, book, timeline, relationships, map);
        var viewsDir = exporter.ExportDirectory(dataRoot, book);
        var required = new[] { "timeline.mmd", "relationships.mmd", "map.mmd", "manifest.json" };
        foreach (var file in required)
        {
            var path = Path.Combine(viewsDir, file);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Calypso smoke: export missing {path}");
                return 1;
            }
        }

        Console.WriteLine($"Calypso smoke OK — views exported to {viewsDir}");
        return 0;
    }
}
