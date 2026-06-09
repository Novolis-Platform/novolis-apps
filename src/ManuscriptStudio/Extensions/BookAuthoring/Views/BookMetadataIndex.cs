using ManuscriptStudio.Extensions.BookAuthoring.Content;

namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal sealed class BookMetadataIndex
{
    public IReadOnlyList<ChapterMetadataEntry> Build(BookInfo book)
    {
        var entries = new List<ChapterMetadataEntry>();
        foreach (var chapter in book.Chapters)
        {
            var text = File.ReadAllText(chapter.FilePath);
            if (text.StartsWith('\uFEFF'))
                text = text[1..];
            var tags = ChapterMetadata.ParseFromMarkdown(text);
            entries.Add(new ChapterMetadataEntry(
                chapter.SortKey,
                chapter.Id,
                chapter.Title,
                chapter.FilePath,
                tags));
        }

        return entries;
    }
}
