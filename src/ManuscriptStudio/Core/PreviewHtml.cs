namespace ManuscriptStudio.Core;

internal static class PreviewHtml
{
    public static string Wrap(string bodyHtml) =>
        "<html><head><style>" +
        "body { font-family: Segoe UI, sans-serif; font-size: 14px; line-height: 1.5; color: #e8e8e8; }" +
        "h1,h2,h3,h4,h5,h6 { color: #f5f5f5; }" +
        "code, pre { font-family: Consolas, monospace; background: #2a2a2a; }" +
        "pre { padding: 8px; border-radius: 4px; }" +
        "blockquote { border-left: 3px solid #666; margin-left: 0; padding-left: 12px; color: #ccc; }" +
        "blockquote.chapter-metadata { border-color: #4a7a9a; background: #1a2430; padding: 8px; }" +
        "table { border-collapse: collapse; }" +
        "th, td { border: 1px solid #555; padding: 4px 8px; }" +
        "a { color: #6eb5ff; }" +
        "</style></head><body>" + bodyHtml + "</body></html>";
}
