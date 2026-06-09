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

    public static string WrapBody(string bodyHtml, MarkdownPreviewTheme theme, double zoomScale)
    {
        var fontSize = MarkdownZoom.ScaledFontSize(BaseFontSize, zoomScale);
        return BuildDocument(bodyHtml, theme, fontSize);
    }

    private static string BuildDocument(string bodyHtml, MarkdownPreviewTheme theme, double fontSizePx)
    {
        var css = theme == MarkdownPreviewTheme.GitHubLight
            ? MarkdownStudioCss.GitHubLight(fontSizePx)
            : MarkdownStudioCss.Dark(fontSizePx);

        var bodyClass = theme == MarkdownPreviewTheme.GitHubLight
            ? "markdown-body github-light"
            : "markdown-body studio";

        var bg = theme == MarkdownPreviewTheme.GitHubLight ? "#ffffff" : "#1e1e1e";
        var fg = theme == MarkdownPreviewTheme.GitHubLight ? "#24292f" : "#e8e8e8";
        var bodyStyle = "background-color:" + bg + ";color:" + fg + ";margin:0;padding:0;";

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><style>" +
               css +
               "</style></head><body class=\"" + bodyClass + "\" style=\"" + bodyStyle + "\">" +
               bodyHtml +
               "</body></html>";
    }
}
