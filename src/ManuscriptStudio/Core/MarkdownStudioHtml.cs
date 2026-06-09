using Markdig;
using Novolis.Avalonia.Markdown;

namespace ManuscriptStudio.Core;

internal static class MarkdownStudioHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private const double BaseFontSize = 15.0;

    public static string BodyFromMarkdown(string? markdown) =>
        string.IsNullOrEmpty(markdown) ? "<p></p>" : Markdown.ToHtml(markdown, Pipeline);

    public static string FromMarkdown(string? markdown, MarkdownPreviewTheme theme, double zoomScale) =>
        WrapBody(BodyFromMarkdown(markdown), theme, zoomScale);

    public static string WrapBody(string bodyHtml, MarkdownPreviewTheme theme, double zoomScale) =>
        BuildDocument(bodyHtml, theme, zoomScale);

    private static string BuildDocument(string bodyHtml, MarkdownPreviewTheme theme, double zoomScale)
    {
        var css = theme == MarkdownPreviewTheme.GitHubLight
            ? MarkdownStudioCss.GitHubLight
            : MarkdownStudioCss.Dark;

        var bodyClass = theme == MarkdownPreviewTheme.GitHubLight
            ? "markdown-body github-light"
            : "markdown-body studio";

        var fontSize = Novolis.Avalonia.Markdown.MarkdownZoom.ScaledFontSize(BaseFontSize, zoomScale);

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><style>" +
               ":root { --ms-base-size: " + fontSize + "px; }" +
               css +
               "</style></head><body class=\"" + bodyClass + "\">" +
               bodyHtml +
               "</body></html>";
    }
}
