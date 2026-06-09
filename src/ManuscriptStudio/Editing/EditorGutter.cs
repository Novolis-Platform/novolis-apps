using System.Text;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace ManuscriptStudio.Editing;

internal static class EditorGutter
{
    public static string Build(
        string? text,
        Typeface typeface,
        double fontSize,
        double contentWidth,
        bool wordWrap,
        int activeLogicalLine)
    {
        var logicalLineCount = CountLogicalLines(text);
        if (!wordWrap || contentWidth <= 1 || string.IsNullOrEmpty(text))
            return Format(logicalLineCount, activeLogicalLine);

        var visualLinesPerLogical = CountVisualLinesPerLogicalLine(text, typeface, fontSize, contentWidth);
        return FormatWrapped(visualLinesPerLogical, activeLogicalLine);
    }

    public static int LineAtCaret(string? text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text) || caretIndex <= 0)
            return 1;

        var line = 1;
        var limit = Math.Min(caretIndex, text.Length);
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
                line++;
        }

        return line;
    }

    public static int CountLogicalLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        var lines = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
                lines++;
        }

        return lines;
    }

    private static string Format(int lineCount, int activeLine = 0)
    {
        lineCount = Math.Max(1, lineCount);
        var width = lineCount.ToString().Length;
        var builder = new StringBuilder(lineCount * (width + 2));

        for (var line = 1; line <= lineCount; line++)
        {
            if (line > 1)
                builder.Append('\n');

            var number = line.ToString().PadLeft(width);
            builder.Append(line == activeLine ? $">{number}" : $" {number}");
        }

        return builder.ToString();
    }

    private static string FormatWrapped(IReadOnlyList<int> visualLinesPerLogicalLine, int activeLogicalLine = 0)
    {
        if (visualLinesPerLogicalLine.Count == 0)
            return Format(1, activeLogicalLine);

        var logicalLineCount = visualLinesPerLogicalLine.Count;
        var width = logicalLineCount.ToString().Length;
        var builder = new StringBuilder();
        var firstRow = true;

        for (var logical = 0; logical < logicalLineCount; logical++)
        {
            var visualCount = Math.Max(1, visualLinesPerLogicalLine[logical]);
            var lineNumber = logical + 1;
            var number = lineNumber.ToString().PadLeft(width);
            var isActive = lineNumber == activeLogicalLine;

            for (var visual = 0; visual < visualCount; visual++)
            {
                if (!firstRow)
                    builder.Append('\n');
                firstRow = false;

                builder.Append(visual == 0
                    ? isActive ? $">{number}" : $" {number}"
                    : new string(' ', width + 1));
            }
        }

        return builder.ToString();
    }

    private static int[] CountVisualLinesPerLogicalLine(
        string text,
        Typeface typeface,
        double fontSize,
        double contentWidth)
    {
        var logicalLines = SplitLogicalLines(text);
        var counts = new int[logicalLines.Count];

        using var layout = new TextLayout(
            text,
            typeface,
            fontSize,
            textWrapping: TextWrapping.Wrap,
            maxWidth: contentWidth);

        if (layout.TextLines.Count == 0)
        {
            Array.Fill(counts, 1);
            return counts;
        }

        for (var i = 0; i < logicalLines.Count; i++)
            counts[i] = 0;

        foreach (var textLine in layout.TextLines)
        {
            var logicalIndex = LogicalLineIndex(logicalLines, textLine.FirstTextSourceIndex);
            counts[logicalIndex]++;
        }

        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] == 0)
                counts[i] = 1;
        }

        return counts;
    }

    private static int LogicalLineIndex(IReadOnlyList<LogicalLineSpan> lines, int charIndex)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (charIndex >= lines[i].Start)
                return i;
        }

        return 0;
    }

    private static List<LogicalLineSpan> SplitLogicalLines(string text)
    {
        var lines = new List<LogicalLineSpan>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            lines.Add(new LogicalLineSpan(start, i - start));
            start = i + 1;
        }

        lines.Add(new LogicalLineSpan(start, text.Length - start));
        return lines;
    }

    private readonly record struct LogicalLineSpan(int Start, int Length);
}
