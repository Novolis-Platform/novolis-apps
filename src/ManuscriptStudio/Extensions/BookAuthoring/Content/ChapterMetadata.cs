using System.Text.RegularExpressions;

namespace ManuscriptStudio.Extensions.BookAuthoring.Content;

internal static class ChapterMetadata
{
    private static readonly Regex TagOpenings = new(@"\[!([a-z0-9_-]+)\]\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> PublicTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "time", "system", "location",
    };

    public static bool IsPublicTag(string tag) => PublicTags.Contains(tag);

    public static List<(string Tag, string Value)> ParseFromMarkdown(string markdown)
    {
        var rows = new List<(string, string)>();
        foreach (var line in markdown.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith('>'))
                continue;

            var plain = trimmed.TrimStart('>').Trim();
            if (plain.Length == 0)
                continue;

            rows.AddRange(SplitFieldsFromPlain(plain));
        }

        return rows;
    }

    public static List<(string Tag, string Value)> FilterForBuild(List<(string Tag, string Value)> rows, bool debugMode)
    {
        IEnumerable<(string Tag, string Value)> q = rows.Where(r => !string.IsNullOrWhiteSpace(r.Value));
        if (!debugMode)
            q = q.Where(r => IsPublicTag(r.Tag));
        return q.ToList();
    }

    public static string FormatInsertLine(string tag, string value) => $"> [!{tag}] {value}";

    private static List<(string Tag, string Value)> SplitFieldsFromPlain(string plain)
    {
        var list = new List<(string, string)>();
        plain = plain.Trim();
        if (plain.Length == 0)
            return list;

        var matches = TagOpenings.Matches(plain);
        if (matches.Count == 0 || matches[0].Index != 0)
            return list;

        for (var i = 0; i < matches.Count; i++)
        {
            var tag = matches[i].Groups[1].Value.ToLowerInvariant();
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : plain.Length;
            var val = plain.Substring(start, end - start).Trim();
            if (val.Length > 0)
                list.Add((tag, val));
        }

        return list;
    }
}
