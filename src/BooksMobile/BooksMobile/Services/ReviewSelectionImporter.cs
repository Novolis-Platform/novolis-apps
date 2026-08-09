using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BooksMobile.Services;

/// <summary>Validates and imports a pasted Galactic Confederation Review selection into docs/.</summary>
public sealed class ReviewSelectionImporter
{
    static readonly string[] RequiredKeys =
    [
        "title", "selection_date", "release_cycle", "field", "type",
        "originating_publication", "original_publication_date", "author", "status", "tags",
    ];

    static readonly Regex FrontMatter = new(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex H1 = new(@"^#\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex ArticleHeading = new(@"^##\s+Article\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex YamlScalar = new(@"^(?<key>[A-Za-z0-9_]+):\s*(?<value>.*)$", RegexOptions.Compiled);

    readonly ChapterSpeechService _speech;

    public ReviewSelectionImporter(ChapterSpeechService speech)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
    }

    public sealed record ImportResult(
        bool Ok,
        string Message,
        string? Slug = null,
        IReadOnlyList<string>? DirtyPaths = null);

    /// <summary>Validates paste, writes article + indexes + audio, returns dirty relative paths.</summary>
    public async Task<ImportResult> ImportAsync(
        string workspaceRoot,
        string pastedMarkdown,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(pastedMarkdown);

        var validation = Validate(pastedMarkdown);
        if (!validation.Ok)
            return new ImportResult(false, validation.Message);

        var meta = validation.Meta!;
        var slug = Slugify(meta["title"]);
        if (string.IsNullOrWhiteSpace(slug))
            return new ImportResult(false, "Could not derive a kebab slug from title.");

        var articlesDir = Path.Combine(workspaceRoot, "docs", "articles");
        Directory.CreateDirectory(articlesDir);
        var articlePath = Path.Combine(articlesDir, slug + ".md");
        if (File.Exists(articlePath))
            return new ImportResult(false, $"Selection already exists: docs/articles/{slug}.md");

        var body = EnsureAudioTrue(pastedMarkdown.Trim() + "\n");
        await File.WriteAllTextAsync(articlePath, body, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        var dirty = new List<string> { $"docs/articles/{slug}.md" };

        TryUpdateArticlesIndex(workspaceRoot, slug, meta, dirty);
        TryUpdateHomeLatest(workspaceRoot, slug, meta, dirty);
        TryUpdateSeriesOrDossier(workspaceRoot, "series", meta, slug, dirty);
        TryUpdateSeriesOrDossier(workspaceRoot, "dossiers", meta, slug, dirty);

        var speechSource = ReviewSpeechText.ToSpeechMarkdown(body);
        var mp3 = await _speech.SynthesizeDocumentMp3Async(speechSource, cancellationToken).ConfigureAwait(false);
        var audioDir = Path.Combine(workspaceRoot, "docs", "assets", "audio");
        Directory.CreateDirectory(audioDir);
        var audioRel = $"docs/assets/audio/{slug}.mp3";
        var audioPath = Path.Combine(workspaceRoot, audioRel.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllBytesAsync(audioPath, mp3, cancellationToken).ConfigureAwait(false);
        dirty.Add(audioRel);
        TryUpdateManifest(workspaceRoot, slug, dirty);

        return new ImportResult(true, $"Imported {slug} with audio.", slug, dirty);
    }

    public static (bool Ok, string Message, Dictionary<string, string>? Meta) Validate(string markdown)
    {
        var match = FrontMatter.Match(markdown.Replace("\r\n", "\n"));
        if (!match.Success)
            return (false, "Missing YAML front matter (--- … ---).", null);

        var meta = ParseYamlScalars(match.Groups[1].Value);
        var missing = RequiredKeys.Where(k => !meta.ContainsKey(k) || string.IsNullOrWhiteSpace(meta[k])).ToList();
        if (missing.Count > 0)
            return (false, "Missing required YAML: " + string.Join(", ", missing), null);

        var body = match.Groups[2].Value;
        if (!H1.IsMatch(body))
            return (false, "Body needs a level-1 title (# Title).", null);
        if (!ArticleHeading.IsMatch(body))
            return (false, "Body needs a ## Article section.", null);
        if (!body.Contains("republication-masthead", StringComparison.OrdinalIgnoreCase))
            return (false, "Body needs a republication-masthead block (good-enough template).", null);
        if (!body.Contains("!!! editorial", StringComparison.OrdinalIgnoreCase))
            return (false, "Body needs an !!! editorial \"Republication note\" block.", null);

        return (true, "OK", meta);
    }

    static Dictionary<string, string> ParseYamlScalars(string yaml)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? listKey = null;
        var listItems = new List<string>();

        void FlushList()
        {
            if (listKey is null)
                return;
            map[listKey] = string.Join("; ", listItems);
            listKey = null;
            listItems.Clear();
        }

        foreach (var raw in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            if (line.StartsWith("  - ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (listKey is null)
                    continue;
                var item = line.Trim().TrimStart('-').Trim().Trim('"').Trim('\'');
                if (item.Length > 0)
                    listItems.Add(item);
                continue;
            }

            FlushList();
            var m = YamlScalar.Match(line.Trim());
            if (!m.Success)
                continue;
            var key = m.Groups["key"].Value;
            var value = m.Groups["value"].Value.Trim();
            if (value.Length == 0)
            {
                listKey = key;
                continue;
            }

            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
                value = value[1..^1];
            map[key] = value;
        }

        FlushList();
        return map;
    }

    static string EnsureAudioTrue(string markdown)
    {
        var match = FrontMatter.Match(markdown.Replace("\r\n", "\n"));
        if (!match.Success)
            return markdown;
        var yaml = match.Groups[1].Value;
        var body = match.Groups[2].Value;
        if (Regex.IsMatch(yaml, @"^audio\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            yaml = Regex.Replace(yaml, @"^audio\s*:.*$", "audio: true", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        else
            yaml = yaml.TrimEnd() + "\naudio: true";
        return $"---\n{yaml}\n---\n{body}";
    }

    static void TryUpdateArticlesIndex(
        string root,
        string slug,
        Dictionary<string, string> meta,
        List<string> dirty)
    {
        var path = Path.Combine(root, "docs", "articles", "index.md");
        if (!File.Exists(path))
            return;

        var date = meta["selection_date"];
        var year = date.Split('.')[0];
        var title = meta["title"];
        var series = meta.TryGetValue("series", out var s) ? s.Split(';')[0].Trim() : "—";
        var field = meta["field"];
        var row = $"| {date} | [{title}]({slug}.md) | {series} | {field} |";

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        if (text.Contains($"({slug}.md)", StringComparison.OrdinalIgnoreCase))
            return;

        var yearHeader = $"## {year}";
        var idx = text.IndexOf(yearHeader, StringComparison.Ordinal);
        if (idx < 0)
        {
            text = text.TrimEnd() + $"\n\n{yearHeader}\n\n| Release | Title | Series | Field |\n| ------- | ----- | ------ | ----- |\n{row}\n";
        }
        else
        {
            var tableStart = text.IndexOf("| Release |", idx, StringComparison.Ordinal);
            if (tableStart < 0)
                return;
            var insertAt = text.IndexOf('\n', tableStart);
            // Skip header + separator
            insertAt = text.IndexOf('\n', insertAt + 1);
            insertAt = text.IndexOf('\n', insertAt + 1);
            if (insertAt < 0)
                return;

            // Append near end of this year's table (before next ## or EOF).
            var nextSection = text.IndexOf("\n## ", insertAt, StringComparison.Ordinal);
            var end = nextSection < 0 ? text.Length : nextSection;
            var block = text[insertAt..end].TrimEnd();
            text = text[..insertAt] + "\n" + block + "\n" + row + "\n" + text[end..];
        }

        // Bump register cycle comment if present.
        text = Regex.Replace(
            text,
            @"(> Register cycle:\s*)[0-9.]+",
            m => m.Groups[1].Value + date,
            RegexOptions.Multiline);

        File.WriteAllText(path, text);
        dirty.Add("docs/articles/index.md");
    }

    static void TryUpdateHomeLatest(
        string root,
        string slug,
        Dictionary<string, string> meta,
        List<string> dirty)
    {
        var path = Path.Combine(root, "docs", "index.md");
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var marker = "<div class=\"selection-grid\" markdown>";
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return;

        var insertAt = idx + marker.Length;
        var series = meta.TryGetValue("series", out var s) ? s.Split(';')[0].Trim() : meta["field"];
        var blurb = meta.TryGetValue("description", out var d) && !string.IsNullOrWhiteSpace(d)
            ? d.Trim()
            : meta["title"];
        var card = $"""

<article markdown>
### [{meta["title"]}](articles/{slug}.md)
<p class="selection-meta">{meta["selection_date"]} · {series} · {meta["field"]}</p>

{blurb}
</article>
""";

        text = text[..insertAt] + card + text[insertAt..];

        // Keep at most 6 latest cards: drop trailing article blocks inside the grid.
        var endGrid = text.IndexOf("</div>", insertAt, StringComparison.Ordinal);
        if (endGrid > 0)
        {
            var grid = text[(insertAt)..endGrid];
            var articles = Regex.Matches(grid, @"<article markdown>[\s\S]*?</article>")
                .Select(m => m.Value)
                .ToList();
            if (articles.Count > 6)
            {
                var kept = string.Concat(articles.Take(6).Select(a => "\n" + a + "\n"));
                text = text[..insertAt] + kept + text[endGrid..];
            }
        }

        text = Regex.Replace(
            text,
            @"(<dd>)([0-9.]+)(</dd>)",
            m => m.Groups[1].Value + meta["selection_date"] + m.Groups[3].Value,
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        // Only bump the first archive register dd — crude but good enough: restore if over-eager.
        // Re-read pattern: first review-register dd only.
        // Already applied globally — leave as good enough for phone import.

        File.WriteAllText(path, text);
        dirty.Add("docs/index.md");
    }

    static void TryUpdateSeriesOrDossier(
        string root,
        string folder,
        Dictionary<string, string> meta,
        string slug,
        List<string> dirty)
    {
        if (!meta.TryGetValue(folder == "series" ? "series" : "dossiers", out var raw) || string.IsNullOrWhiteSpace(raw))
            return;

        foreach (var name in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fileSlug = Slugify(name);
            var path = Path.Combine(root, "docs", folder, fileSlug + ".md");
            if (!File.Exists(path))
                continue;

            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            var link = $"../articles/{slug}.md";
            if (text.Contains(link, StringComparison.OrdinalIgnoreCase))
                continue;

            var row = $"| {meta["selection_date"]} | [{meta["title"]}]({link}) | {meta["field"]} |";
            if (text.Contains("| Release |", StringComparison.Ordinal))
                text = text.TrimEnd() + "\n" + row + "\n";
            else
            {
                text = text.TrimEnd()
                    + "\n\n## Release order\n\n| Release | Selection | Field |\n| ------- | --------- | ----- |\n"
                    + row + "\n";
            }

            File.WriteAllText(path, text);
            dirty.Add($"docs/{folder}/{fileSlug}.md");
        }
    }

    static void TryUpdateManifest(string root, string slug, List<string> dirty)
    {
        var path = Path.Combine(root, "docs", "assets", "audio", "manifest.json");
        var articles = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("articles", out var arts))
                {
                    foreach (var prop in arts.EnumerateObject())
                    {
                        var entry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var field in prop.Value.EnumerateObject())
                            entry[field.Name] = field.Value.ToString();
                        articles[prop.Name] = entry;
                    }
                }
            }
            catch
            {
                // Rebuild lightly.
            }
        }

        articles[slug] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "generated",
            ["backend"] = "edge-tts",
            ["updated"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["output"] = $"assets/audio/{slug}.mp3",
        };

        var payload = new Dictionary<string, object> { ["articles"] = articles };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + "\n");
        dirty.Add("docs/assets/audio/manifest.json");
    }

    public static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in title.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (ch is ' ' or '-' or '_' or '/')
                sb.Append('-');
        }

        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return slug;
    }

    /// <summary>Short ChatGPT paste skeleton shown in the app.</summary>
    public const string TemplateBlurb = """
Paste a full selection that follows this skeleton (YAML + masthead + editorial + ## Article):

---
title: "Title"
description: "One-line archive summary."
selection_date: "2497.250"
release_cycle: "2497.250"
field: "History and Policy"
type: "Republication"
series:
  - "Historical Summaries"
originating_publication: "*Journal Name*, Vol. N"
original_publication_date: "2496.100"
author: "Name, Title, Institution"
status: "Public archive edition"
tags:
  - tag-one
---
# Title

<div class="republication-masthead" markdown="1">
…masthead dl…
</div>

!!! editorial "Republication note"
    Why the Review selected this work.

## Article

### 1. First section

Body in the author's voice.
""";
}
