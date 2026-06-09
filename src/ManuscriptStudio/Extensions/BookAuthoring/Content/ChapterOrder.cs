using System.Globalization;
using System.Text.RegularExpressions;

namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal static class ChapterOrder
{
    private static readonly Regex BooktoolsComment = new(@"<!--\s*booktools-chapter:\s*([\d.]+)\s*-->", RegexOptions.Compiled);
    private static readonly Regex FrontMatter = new(@"(?s)^---\s*\r?\n(.*?)\r?\n---\s*\r?\n", RegexOptions.Compiled);
    private static readonly Regex YamlChapter = new(@"^\s*chapter:\s*([\d.]+)\s*(?:#.*)?$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex HeadingChapter = new(@"^\s*#\s*Chapter\s+(\d+(?:\.\d+)?)\s*-\s*(.+)\s*$", RegexOptions.Compiled);
    private static readonly Regex HeadingTitleOnly = new(@"^\s*#\s*Chapter\s+\d+(?:\.\d+)?\s*-\s*(.+)\s*$", RegexOptions.Compiled);
    private static readonly Regex HeadingGeneric = new(@"^\s*#\s+(.+)\s*$", RegexOptions.Compiled);

    public static double GetSortKey(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals("00-frontmatter.md", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("-frontmatter.md", StringComparison.OrdinalIgnoreCase))
            return -1;

        var raw = File.ReadAllText(filePath);
        if (raw.StartsWith('\uFEFF'))
            raw = raw[1..];

        var lines = raw.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (var i = 0; i < Math.Min(20, lines.Length); i++)
        {
            var cm = BooktoolsComment.Match(lines[i]);
            if (cm.Success)
                return double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var fm = FrontMatter.Match(raw);
        if (fm.Success)
        {
            var ym = YamlChapter.Match(fm.Groups[1].Value);
            if (ym.Success)
                return double.Parse(ym.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        foreach (var line in lines)
        {
            var hm = HeadingChapter.Match(line);
            if (hm.Success)
                return double.Parse(hm.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return double.PositiveInfinity;
    }

    public static string? ReadChapterTitle(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var m = HeadingTitleOnly.Match(line);
            if (m.Success)
                return m.Groups[1].Value.Trim();
            m = HeadingGeneric.Match(line);
            if (m.Success)
                return m.Groups[1].Value.Trim();
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }

    public static IReadOnlyList<ChapterInfo> SortChapters(IReadOnlyList<ChapterInfo> chapters, bool orderFromHeading) =>
        orderFromHeading
            ? chapters.OrderBy(c => c.Kind).ThenBy(c => c.SortKey).ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase).ToList()
            : chapters.OrderBy(c => c.Kind).ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
}
