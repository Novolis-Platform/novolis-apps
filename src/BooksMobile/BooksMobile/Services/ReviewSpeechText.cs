using System.Text.RegularExpressions;

namespace BooksMobile.Services;

/// <summary>
/// Converts Galactic Confederation Review markdown into narration-ready text
/// (C# port of the Review scripts/lib/speech_text.py intent — no Python).
/// </summary>
public static class ReviewSpeechText
{
    static readonly Regex FrontMatter = new(@"^---\s*\n.*?\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex HtmlTag = new(@"<[^>]+>", RegexOptions.Compiled);
    static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    static readonly Regex Image = new(@"!\[([^\]]*)\]\([^)]+\)", RegexOptions.Compiled);
    static readonly Regex Bold = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
    static readonly Regex Italic = new(@"(?<!\*)\*([^*]+)\*(?!\*)", RegexOptions.Compiled);
    static readonly Regex CodeFence = new(@"```.*?```", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex InlineCode = new(@"`([^`]+)`", RegexOptions.Compiled);
    static readonly Regex Heading = new(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex Admonition = new(
        @"^!!!\s+(\w+)(?:\s+""([^""]+)"")?\s*\n((?:    .+\n?)*)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex HtmlBlock = new(
        @"<div[^>]*markdown=""[^""]*""[^>]*>\s*\n?(.*?)\n?</div>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Returns plain speech text suitable for TTS planning.</summary>
    public static string ToSpeechMarkdown(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var text = SanitizeBody(markdown);
        // Wrap as a simple markdown body so SpeechPlanner keeps the title when speakTitle is used.
        return "# Selection\n\n" + text;
    }

    /// <summary>
    /// Preview-safe markdown for HtmlPanel: strips Review masthead HTML / admonition chrome
    /// that has crashed Avalonia HTML rendering on Android.
    /// </summary>
    public static string ToPreviewMarkdown(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var match = System.Text.RegularExpressions.Regex.Match(
            markdown.Replace("\r\n", "\n"),
            @"^---\s*\n.*?\n---\s*\n(.*)$",
            RegexOptions.Singleline);
        var body = match.Success ? match.Groups[1].Value : markdown;
        var title = ExtractH1(body);
        body = StripAdmonitions(body);
        body = StripHtmlBlocks(body);
        body = HtmlTag.Replace(body, "");
        body = CollapseWhitespace(body);
        if (!string.IsNullOrWhiteSpace(title))
            return $"# {title}\n\n{body}";
        return body;
    }

    static string SanitizeBody(string markdown)
    {
        var text = FrontMatter.Replace(markdown, "", 1);
        text = StripAdmonitions(text);
        text = StripHtmlBlocks(text);
        text = CodeFence.Replace(text, " ");
        text = Image.Replace(text, "");
        text = Link.Replace(text, "$1");
        text = Bold.Replace(text, "$1");
        text = Italic.Replace(text, "$1");
        text = InlineCode.Replace(text, "$1");
        text = NormalizeHeadings(text);
        text = HtmlTag.Replace(text, "");
        return CollapseWhitespace(text);
    }

    static string? ExtractH1(string body)
    {
        var m = Regex.Match(body, @"^#\s+(.+)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    static string StripAdmonitions(string text)
    {
        return Admonition.Replace(text, match =>
        {
            var title = match.Groups[2].Success && match.Groups[2].Length > 0
                ? match.Groups[2].Value
                : match.Groups[1].Value.Replace('_', ' ');
            var body = Regex.Replace(match.Groups[3].Value, @"^    ", "", RegexOptions.Multiline).Trim();
            return $"{title}. {body}\n\n";
        });
    }

    static string StripHtmlBlocks(string text)
    {
        text = HtmlBlock.Replace(text, match =>
        {
            var inner = HtmlTag.Replace(match.Groups[1].Value, "").Trim();
            return inner + "\n\n";
        });
        return HtmlTag.Replace(text, "");
    }

    static string NormalizeHeadings(string text) =>
        Heading.Replace(text, match =>
        {
            var heading = match.Groups[1].Value.Trim();
            heading = Link.Replace(heading, "$1");
            heading = Italic.Replace(heading, "$1");
            return $"\n{heading}.\n";
        });

    static string CollapseWhitespace(string text)
    {
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        return text.Trim();
    }
}
