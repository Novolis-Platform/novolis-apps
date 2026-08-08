using System.Text.Json;
using Novolis.Manuscript;
using Novolis.Manuscript.IO;

namespace BooksWriterStudio.Services;

/// <summary>Thin host wrappers over <see cref="LegacyChapterSurgery"/> (dry-run then apply).</summary>
internal static class StructureSurgeryActions
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ResolveChaptersDir(BookInfo book) =>
        ManuscriptPaths.ResolveChaptersDirectory(book);

    public static ChapterMutationResult InsertAfterDryRun(string chaptersDir, double afterKey, string title) =>
        LegacyChapterSurgery.InsertAfter(chaptersDir, afterKey, title, apply: false);

    public static ChapterMutationResult InsertAfterApply(string chaptersDir, double afterKey, string title) =>
        LegacyChapterSurgery.InsertAfter(chaptersDir, afterKey, title, apply: true);

    public static ChapterMutationResult SyncFilenamesDryRun(string chaptersDir) =>
        LegacyChapterSurgery.SyncFilenames(chaptersDir, apply: false);

    public static ChapterMutationResult SyncFilenamesApply(string chaptersDir) =>
        LegacyChapterSurgery.SyncFilenames(chaptersDir, apply: true);

    public static string FormatPlan(ChapterMutationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Plan is null)
            return result.Message;
        try
        {
            return $"{result.Message}\n\n{JsonSerializer.Serialize(result.Plan, JsonOptions)}";
        }
        catch
        {
            return $"{result.Message}\n\n{result.Plan}";
        }
    }
}
