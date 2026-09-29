using System.Text.RegularExpressions;
using Novolis.Markup.Markdown;

namespace ReadAloud.Services;

/// <summary>Detects Markdown structures that should not be read as prose.</summary>
internal static class MarkdownDetector
{
    private static readonly Regex TableSeparatorCell = new(
        @"^:?-{3,}:?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Detects a table and returns the first line after it. The shared Markdown
    /// parser validates standard pipe tables; the compatible fallback keeps
    /// support for tables whose first cell is not surrounded by pipes.
    /// </summary>
    public static bool TryReadTable(
        IReadOnlyList<string> lines,
        int startIndex,
        out int nextIndex,
        out string speech)
    {
        nextIndex = startIndex;
        speech = string.Empty;
        if (startIndex < 0 ||
            startIndex + 1 >= lines.Count ||
            !IsTableRow(lines[startIndex].Trim()) ||
            !IsTableSeparator(lines[startIndex + 1].Trim()))
        {
            return false;
        }

        nextIndex = startIndex + 2;
        while (nextIndex < lines.Count && IsTableRow(lines[nextIndex].Trim()))
            nextIndex++;

        var candidate = string.Join(
            '\n',
            lines
                .Skip(startIndex)
                .Take(nextIndex - startIndex)
                .Select(static line => line.Trim()));
        var sharedDocument = MarkdownDocument.Parse(candidate);
        var sharedTable = sharedDocument.OfType<IMarkdownTable>().FirstOrDefault();
        if (sharedTable is not null)
        {
            speech = MarkdownDeserializer.ToSpeech(sharedTable);
            return true;
        }

        if (!IsCompatibleTable(candidate))
            return false;

        speech = MarkdownDeserializer.TablePlaceholder;
        return true;
    }

    private static bool IsCompatibleTable(string candidate) =>
        candidate
            .Split('\n')
            .All(static line => IsTableRow(line) || IsTableSeparator(line));

    private static bool IsTableRow(string line) =>
        line.Contains('|', StringComparison.Ordinal) &&
        SplitTableRow(line).Count > 1;

    private static bool IsTableSeparator(string line) =>
        SplitTableRow(line).Count > 1 &&
        SplitTableRow(line).All(static cell => TableSeparatorCell.IsMatch(cell));

    private static List<string> SplitTableRow(string line)
    {
        var value = line.Trim();
        if (value.StartsWith('|'))
            value = value[1..];
        if (value.EndsWith('|'))
            value = value[..^1];

        return value
            .Split('|')
            .Select(static cell => cell.Trim())
            .ToList();
    }
}
