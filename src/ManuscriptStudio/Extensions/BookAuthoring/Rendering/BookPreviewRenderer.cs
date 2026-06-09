using System.Net;
using System.Text.RegularExpressions;
using ManuscriptStudio.Extensions.BookAuthoring.Content;
using Novolis.Avalonia.Markdown;

namespace ManuscriptStudio.Extensions.BookAuthoring.Rendering;

internal sealed class BookPreviewRenderer
{
    private static readonly Regex MetadataBlockquote = new(@"^>\s*\[!", RegexOptions.Compiled);

    public string ToBodyHtml(string markdown, bool debugMetadata, MarkdownPreviewTheme theme = MarkdownPreviewTheme.StudioDark)
    {
        if (string.IsNullOrEmpty(markdown))
            return "<p></p>";

        var processed = PreprocessMetadataBlockquotes(markdown, debugMetadata);
        return MarkdownPreviewPipeline.ToBodyHtml(processed, theme);
    }

    private static string PreprocessMetadataBlockquotes(string markdown, bool debugMetadata)
    {
        var lines = markdown.Split(["\r\n", "\n"], StringSplitOptions.None);
        var output = new List<string>();
        var i = 0;
        while (i < lines.Length)
        {
            if (!MetadataBlockquote.IsMatch(lines[i].TrimStart()))
            {
                output.Add(lines[i]);
                i++;
                continue;
            }

            var blockLines = new List<string>();
            while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
            {
                blockLines.Add(lines[i]);
                i++;
            }

            var plain = string.Join("\n", blockLines.Select(l => l.TrimStart().TrimStart('>').Trim()));
            var rows = ChapterMetadata.ParseFromMarkdown(plain);
            var visible = ChapterMetadata.FilterForBuild(rows, debugMetadata);
            if (visible.Count == 0)
                continue;

            output.Add("<blockquote class=\"chapter-metadata\">");
            foreach (var (tag, value) in visible)
            {
                var label = debugMetadata ? $"[{tag}] " : string.Empty;
                output.Add($"<p>{WebUtility.HtmlEncode(label + value)}</p>");
            }
            output.Add("</blockquote>");
        }

        return string.Join(Environment.NewLine, output);
    }
}
