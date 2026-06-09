namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal enum ChapterKind
{
    Chapter,
    Appendix,
}

internal sealed record SeriesInfo(string Id, string Title, string DirectoryPath, IReadOnlyList<BookInfo> Books);

internal sealed record BookInfo(
    string Id,
    string Title,
    string? Subtitle,
    string? Author,
    string DirectoryPath,
    string? SeriesId,
    IReadOnlyList<ChapterInfo> Chapters,
    bool ChapterOrderFromHeading,
    bool DebugMode);

internal sealed record ChapterInfo(
    string Id,
    string Title,
    ChapterKind Kind,
    double SortKey,
    string FilePath);
