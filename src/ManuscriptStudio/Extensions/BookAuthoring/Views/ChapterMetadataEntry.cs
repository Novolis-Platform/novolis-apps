namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal sealed record ChapterMetadataEntry(
    double SortKey,
    string ChapterId,
    string Title,
    string FilePath,
    IReadOnlyList<(string Tag, string Value)> Tags);
