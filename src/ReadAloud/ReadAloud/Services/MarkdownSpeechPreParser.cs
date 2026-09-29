using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadAloud.Services;

/// <summary>
/// Converts common Markdown constructs into readable speech paragraphs before
/// the shared speech planner sees the document.
/// </summary>
public static class MarkdownSpeechPreParser
{
    static readonly Regex FrontMatter = new(
        @"\A(?:\uFEFF)?---\s*\n.*?\n---\s*(?:\n|$)",
        RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex HtmlComment = new(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex HtmlNoiseBlock = new(
        @"<(?:script|style|noscript)\b[^>]*>.*?</(?:script|style|noscript)>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex HtmlTag = new(
        @"<[^>]+>",
        RegexOptions.Compiled);
    static readonly Regex Image = new(
        @"!\[([^\]]*)\]\([^)]+\)",
        RegexOptions.Compiled);
    static readonly Regex Link = new(
        @"\[([^\]]+)\]\([^)]+\)",
        RegexOptions.Compiled);
    static readonly Regex ReferenceLink = new(
        @"\[([^\]]+)\]\[[^\]]*\]",
        RegexOptions.Compiled);
    static readonly Regex Autolink = new(
        @"<https?://[^>]+>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RawUrl = new(
        @"(?i)\bhttps?://[^\s<>()]+",
        RegexOptions.Compiled);
    static readonly Regex FootnoteReference = new(
        @"\[\^[^\]]+\]",
        RegexOptions.Compiled);
    static readonly Regex LinkDefinition = new(
        @"^\s*\[[^\]]+\]:\s+\S+",
        RegexOptions.Compiled);
    static readonly Regex Heading = new(
        @"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$",
        RegexOptions.Compiled);
    static readonly Regex ListItem = new(
        @"^\s*(?<marker>[-+*]|\d+[.)])\s+(?<text>.+)$",
        RegexOptions.Compiled);
    static readonly Regex Admonition = new(
        @"^\s*!!!\s+(?<kind>[A-Za-z0-9_-]+)(?:\s+[""']?(?<title>[^""']+)[""']?)?\s*$",
        RegexOptions.Compiled);
    static readonly Regex Alert = new(
        @"^\[!(?<kind>NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*(?<text>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex HorizontalRule = new(
        @"^(?:(?:\*|_|-)\s*){3,}$",
        RegexOptions.Compiled);
    static readonly Regex Fence = new(
        @"^(?<marker>`{3,}|~{3,})(?<language>.*)$",
        RegexOptions.Compiled);
    static readonly Regex Checkbox = new(
        @"^\[(?<state>[ xX])\]\s*",
        RegexOptions.Compiled);
    static readonly Regex TableSeparatorCell = new(
        @"^:?-{3,}:?$",
        RegexOptions.Compiled);

    /// <summary>Returns Markdown transformed into natural speech paragraphs.</summary>
    public static string Normalize(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var source = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        source = FrontMatter.Replace(source, string.Empty, 1);
        source = HtmlComment.Replace(source, string.Empty);
        source = HtmlNoiseBlock.Replace(source, string.Empty);

        var lines = source.Split('\n');
        var blocks = new List<string>();
        var paragraph = new StringBuilder();
        var codeLines = new List<string>();
        string? fenceMarker = null;
        string language = string.Empty;

        void FlushParagraph()
        {
            AddBlock(blocks, paragraph.ToString());
            paragraph.Clear();
        }

        void AddCodeBlock()
        {
            var code = string.Join(' ', codeLines).Trim();
            codeLines.Clear();
            var normalizedLanguage = CleanInline(language);
            language = string.Empty;
            if (normalizedLanguage is "mermaid" or "plantuml" or "dot" or "graphviz")
            {
                AddBlock(blocks, "Diagram omitted.");
                return;
            }

            if (code.Length == 0)
                return;

            var label = normalizedLanguage.Length == 0
                ? "Code block."
                : $"Code block {normalizedLanguage}.";
            AddBlock(blocks, $"{label} {code}");
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].TrimEnd();
            var trimmed = raw.Trim();

            if (fenceMarker is not null)
            {
                if (IsFenceClose(trimmed, fenceMarker))
                {
                    fenceMarker = null;
                    AddCodeBlock();
                }
                else
                {
                    codeLines.Add(trimmed);
                }

                continue;
            }

            var fence = Fence.Match(trimmed);
            if (fence.Success)
            {
                FlushParagraph();
                fenceMarker = fence.Groups["marker"].Value;
                language = fence.Groups["language"].Value.Trim().Split(' ', 2)[0];
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (LinkDefinition.IsMatch(trimmed))
                continue;

            if (HorizontalRule.IsMatch(trimmed))
            {
                FlushParagraph();
                blocks.Add("***");
                continue;
            }

            if (TryReadTable(
                    lines,
                    ref index,
                    trimmed,
                    out var tableBlocks))
            {
                FlushParagraph();
                blocks.AddRange(tableBlocks);
                continue;
            }

            var heading = Heading.Match(raw);
            if (heading.Success)
            {
                FlushParagraph();
                AddBlock(blocks, EnsureSentence(CleanInline(heading.Groups[1].Value)));
                continue;
            }

            var admonition = Admonition.Match(raw);
            if (admonition.Success)
            {
                FlushParagraph();
                var title = string.IsNullOrWhiteSpace(admonition.Groups["title"].Value)
                    ? admonition.Groups["kind"].Value
                    : admonition.Groups["title"].Value;
                AddBlock(blocks, EnsureSentence(CleanInline(title)));
                continue;
            }

            var line = RemoveBlockquotePrefix(trimmed);
            var alert = Alert.Match(line);
            if (alert.Success)
            {
                FlushParagraph();
                var label = CultureTitle(alert.Groups["kind"].Value);
                var alertText = CleanInline(alert.Groups["text"].Value);
                AddBlock(
                    blocks,
                    alertText.Length == 0
                        ? EnsureSentence(label)
                        : $"{EnsureSentence(label)} {alertText}");
                continue;
            }

            if (!string.Equals(line, trimmed, StringComparison.Ordinal))
            {
                FlushParagraph();
                var quote = CleanInline(line);
                if (quote.Length > 0)
                    AddBlock(blocks, $"Quote. {quote}");
                continue;
            }

            var listItem = ListItem.Match(raw);
            if (listItem.Success)
            {
                FlushParagraph();
                var item = listItem.Groups["text"].Value;
                var checkbox = Checkbox.Match(item);
                var prefix = "List item.";
                if (checkbox.Success)
                {
                    prefix = checkbox.Groups["state"].Value is "x" or "X"
                        ? "Completed list item."
                        : "Todo list item.";
                    item = item[checkbox.Length..];
                }

                if (char.IsDigit(listItem.Groups["marker"].Value[0]))
                {
                    prefix = $"List item {listItem.Groups["marker"].Value.TrimEnd('.', ')')}.";
                }

                AddBlock(blocks, $"{prefix} {item}");
                continue;
            }

            var cleanLine = CleanInline(trimmed);
            if (cleanLine.Length > 0)
            {
                if (paragraph.Length > 0)
                    paragraph.Append(' ');
                paragraph.Append(cleanLine);
            }
        }

        if (fenceMarker is not null)
            AddCodeBlock();
        FlushParagraph();
        return string.Join("\n\n", blocks).Trim();
    }

    static bool TryReadTable(
        string[] lines,
        ref int index,
        string current,
        out List<string> blocks)
    {
        blocks = [];
        if (!IsTableRow(current) ||
            index + 1 >= lines.Length ||
            !IsTableSeparator(lines[index + 1]))
        {
            return false;
        }

        var headers = SplitTableRow(current);
        if (headers.Count > 0)
            blocks.Add($"Table. {string.Join(", ", headers)}");

        index += 2;
        while (index < lines.Length && IsTableRow(lines[index].Trim()))
        {
            var cells = SplitTableRow(lines[index]);
            if (cells.Count > 0)
            {
                var values = cells
                    .Select((cell, cellIndex) =>
                        cellIndex < headers.Count && headers[cellIndex].Length > 0
                            ? $"{headers[cellIndex]}: {cell}"
                            : cell)
                    .Where(static value => value.Length > 0);
                blocks.Add($"Table row. {string.Join(". ", values)}");
            }

            index++;
        }

        index--;
        return true;
    }

    static bool IsTableRow(string line) =>
        line.Contains('|', StringComparison.Ordinal) &&
        SplitTableRow(line).Count > 1;

    static bool IsTableSeparator(string line) =>
        SplitTableRow(line).Count > 1 &&
        SplitTableRow(line).All(cell => TableSeparatorCell.IsMatch(cell));

    static List<string> SplitTableRow(string line)
    {
        var value = line.Trim();
        if (value.StartsWith('|'))
            value = value[1..];
        if (value.EndsWith('|'))
            value = value[..^1];

        return value
            .Split('|')
            .Select(cell => CleanInline(cell.Replace("\\|", "|", StringComparison.Ordinal)))
            .ToList();
    }

    static bool IsFenceClose(string line, string marker)
    {
        if (line.Length < marker.Length || line[0] != marker[0])
            return false;

        var length = 0;
        while (length < line.Length && line[length] == marker[0])
            length++;
        return length >= marker.Length;
    }

    static string RemoveBlockquotePrefix(string line) =>
        Regex.Replace(line, @"^(?:>\s*)+", string.Empty);

    static string CultureTitle(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    static string EnsureSentence(string value)
    {
        if (value.Length == 0 ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith("!", StringComparison.Ordinal) ||
            value.EndsWith("?", StringComparison.Ordinal))
        {
            return value;
        }

        return value + ".";
    }

    static void AddBlock(List<string> blocks, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (string.Equals(value.Trim(), "***", StringComparison.Ordinal))
        {
            blocks.Add("***");
            return;
        }

        var clean = CleanInline(value);
        if (clean.Length > 0)
            blocks.Add(clean);
    }

    static string CleanInline(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var clean = WebUtility.HtmlDecode(text);
        clean = Image.Replace(clean, "$1");
        clean = Link.Replace(clean, "$1");
        clean = ReferenceLink.Replace(clean, "$1");
        clean = Autolink.Replace(clean, string.Empty);
        clean = FootnoteReference.Replace(clean, string.Empty);
        clean = RawUrl.Replace(clean, string.Empty);
        clean = HtmlTag.Replace(clean, " ");
        clean = Regex.Replace(clean, @"\\([\\`*{}\[\]()#+.!_>~-])", "$1");
        clean = Regex.Replace(clean, @"[*_~`]+", string.Empty);
        clean = Regex.Replace(clean, @"\s+([,.;:!?])", "$1");
        clean = Regex.Replace(clean, @"[ \t]+", " ");
        return clean.Trim();
    }
}
