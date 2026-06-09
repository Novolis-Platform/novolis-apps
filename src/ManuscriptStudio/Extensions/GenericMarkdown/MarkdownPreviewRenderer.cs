using Markdig;

namespace ManuscriptStudio.Extensions.GenericMarkdown;

internal sealed class MarkdownPreviewRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markdown) =>
        string.IsNullOrEmpty(markdown)
            ? "<p></p>"
            : Markdown.ToHtml(markdown, _pipeline);
}
