using Novolis.Markup.Markdown;

namespace ReadAloud.Services;

/// <summary>
/// Converts Markdown sections that do not have a useful speech representation
/// into short, deterministic spoken placeholders.
/// </summary>
internal static class MarkdownDeserializer
{
    public const string TablePlaceholder = "Table cannot be read.";
    public const string CodeBlockPlaceholder = "Code-block cannot be read.";

    public static string ToSpeech(IMarkdownSection section) =>
        section switch
        {
            IMarkdownTable => TablePlaceholder,
            IMarkdownCodeBlock => CodeBlockPlaceholder,
            _ => string.Empty,
        };
}
