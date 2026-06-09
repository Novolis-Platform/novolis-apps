namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal enum ChapterKind
{
    Chapter,
    Appendix,
}

internal sealed record SeriesInfo(
    string Id,
    string Title,
    string DirectoryPath,
    IReadOnlyList<BookInfo> Books,
    IReadOnlyList<ReferenceSetInfo> References);

internal sealed record BookInfo(
    string Id,
    string Title,
    string? Subtitle,
    string? Author,
    string DirectoryPath,
    string? SeriesId,
    IReadOnlyList<ChapterInfo> Chapters,
    bool ChapterOrderFromHeading,
    bool DebugMode,
    IReadOnlyList<ReferenceSetInfo> References);

internal sealed record ReferenceSetInfo(
    string Id,
    string Title,
    string DirectoryPath,
    IReadOnlyList<ReferenceFileInfo> Files);

internal sealed record ReferenceFileInfo(string Id, string Title, string FilePath);

internal sealed record ChapterInfo(
    string Id,
    string Title,
    ChapterKind Kind,
    double SortKey,
    string FilePath);
