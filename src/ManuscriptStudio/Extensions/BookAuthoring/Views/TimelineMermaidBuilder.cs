using System.Text;
using ManuscriptStudio.Extensions.BookAuthoring.Content;

namespace ManuscriptStudio.Extensions.BookAuthoring.Views;

internal static class TimelineMermaidBuilder
{
    public static string Build(BookInfo book, IReadOnlyList<ChapterMetadataEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timeline");
        sb.AppendLine($"    title {Escape(book.Title)} — chapter dates");
        sb.AppendLine("    section By chapter order");

        foreach (var entry in entries.OrderBy(e => e.SortKey))
        {
            var date = entry.Tags.FirstOrDefault(t => t.Tag.Equals("date", StringComparison.OrdinalIgnoreCase)).Value;
            var label = $"{entry.SortKey:0.###} {entry.Title}";
            if (!string.IsNullOrWhiteSpace(date))
                sb.AppendLine($"        {date.Trim()} : {Escape(label)}");
            else
                sb.AppendLine($"        ch-{entry.SortKey:0} : {Escape(label)}");
        }

        var povGroups = entries
            .Select(e => (Pov: GetTag(e, "pov"), Entry: e))
            .Where(x => !string.IsNullOrWhiteSpace(x.Pov))
            .GroupBy(x => x.Pov!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in povGroups)
        {
            sb.AppendLine($"    section POV {Escape(group.Key)}");
            foreach (var (_, entry) in group.OrderBy(x => x.Entry.SortKey))
            {
                var date = GetTag(entry, "date") ?? $"ch-{entry.SortKey:0}";
                sb.AppendLine($"        {date} : {Escape(entry.Title)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string? GetTag(ChapterMetadataEntry entry, string tag) =>
        entry.Tags.FirstOrDefault(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)).Value;

    private static string Escape(string text) => text.Replace("\"", "'");
}
