namespace ManuscriptStudio.Core;

internal static class MarkdownStudioCss
{
    public static string Dark(double fontSizePx) => $$$"""
        html, body {
          margin: 0;
          padding: 0;
          background-color: #1e1e1e !important;
          color: #e8e8e8 !important;
          overflow-x: hidden;
        }
        .markdown-body.studio, .markdown-body.studio p, .markdown-body.studio li,
        .markdown-body.studio td, .markdown-body.studio th, .markdown-body.studio blockquote,
        .markdown-body.studio span, .markdown-body.studio div {
          color: #e8e8e8 !important;
        }
        .markdown-body.studio {
          font-family: "Segoe UI", system-ui, sans-serif;
          font-size: {{{fontSizePx}}}px;
          line-height: 1.65;
          padding: 16px 20px 28px;
          max-width: 100%;
          word-wrap: break-word;
          overflow-wrap: break-word;
          background-color: #1e1e1e !important;
        }
        .markdown-body.studio h1, .markdown-body.studio h2, .markdown-body.studio h3,
        .markdown-body.studio h4, .markdown-body.studio h5, .markdown-body.studio h6 {
          color: #f5f5f5 !important;
          font-weight: 600;
          margin-top: 1.4em;
          margin-bottom: 0.6em;
          line-height: 1.25;
        }
        .markdown-body.studio h1 { font-size: 1.75em; border-bottom: 1px solid #3a3a3a; padding-bottom: 0.25em; }
        .markdown-body.studio h2 { font-size: 1.4em; border-bottom: 1px solid #333; padding-bottom: 0.2em; }
        .markdown-body.studio h3 { font-size: 1.2em; }
        .markdown-body.studio p { margin: 0 0 0.9em; }
        .markdown-body.studio a { color: #6eb5ff !important; text-decoration: none; word-break: break-all; }
        .markdown-body.studio a:hover { text-decoration: underline; }
        .markdown-body.studio code {
          font-family: "Cascadia Code", Consolas, "Courier New", monospace;
          font-size: 0.92em;
          background-color: #2a2a2a !important;
          color: #e8e8e8 !important;
          padding: 0.15em 0.35em;
          border-radius: 4px;
          word-break: break-word;
        }
        .markdown-body.studio pre {
          font-family: "Cascadia Code", Consolas, "Courier New", monospace;
          font-size: 0.9em;
          background-color: #252526 !important;
          color: #e8e8e8 !important;
          border: 1px solid #3a3a3a;
          border-radius: 6px;
          padding: 12px 14px;
          overflow-x: auto;
          margin: 0 0 1em;
          white-space: pre-wrap;
          word-break: break-word;
        }
        .markdown-body.studio pre code { background: transparent !important; padding: 0; white-space: pre-wrap; }
        .markdown-body.studio blockquote {
          margin: 0 0 1em;
          padding: 8px 14px;
          border-left: 3px solid #4a7a9a;
          background-color: #1a2430 !important;
          color: #c8d4e0 !important;
        }
        .markdown-body.studio blockquote.chapter-metadata {
          border-left-color: #5a9aba;
          background-color: #152028 !important;
        }
        .markdown-body.studio ul, .markdown-body.studio ol {
          margin: 0 0 1em;
          padding-left: 1.6em;
        }
        .markdown-body.studio li { margin: 0.2em 0; }
        .markdown-body.studio table {
          border-collapse: collapse;
          width: 100%;
          margin: 0 0 1em;
          display: block;
          overflow-x: auto;
        }
        .markdown-body.studio th, .markdown-body.studio td {
          border: 1px solid #444;
          padding: 6px 10px;
          text-align: left;
          word-break: break-word;
        }
        .markdown-body.studio th { background-color: #2d2d30 !important; font-weight: 600; }
        .markdown-body.studio tr:nth-child(even) td { background-color: #252526 !important; }
        .markdown-body.studio hr {
          border: none;
          border-top: 1px solid #3a3a3a;
          margin: 1.5em 0;
        }
        .markdown-body.studio img { max-width: 100%; height: auto; }
        """;

    public static string GitHubLight(double fontSizePx) => $$$"""
        html, body {
          margin: 0;
          padding: 0;
          background-color: #ffffff !important;
          color: #24292f !important;
          overflow-x: hidden;
        }
        .markdown-body.github-light, .markdown-body.github-light p, .markdown-body.github-light li,
        .markdown-body.github-light td, .markdown-body.github-light th {
          color: #24292f !important;
        }
        .markdown-body.github-light {
          font-family: "Segoe UI", system-ui, sans-serif;
          font-size: {{{fontSizePx}}}px;
          line-height: 1.65;
          padding: 16px 20px 28px;
          max-width: 100%;
          word-wrap: break-word;
          overflow-wrap: break-word;
          background-color: #ffffff !important;
        }
        .markdown-body.github-light h1, .markdown-body.github-light h2 {
          border-bottom: 1px solid #d8dee4;
          padding-bottom: 0.25em;
          color: #24292f !important;
        }
        .markdown-body.github-light code {
          font-family: "Cascadia Code", Consolas, monospace;
          background-color: #f6f8fa !important;
          color: #24292f !important;
          padding: 0.15em 0.35em;
          border-radius: 4px;
          word-break: break-word;
        }
        .markdown-body.github-light pre {
          background-color: #f6f8fa !important;
          color: #24292f !important;
          border: 1px solid #d8dee4;
          border-radius: 6px;
          padding: 12px 14px;
          overflow-x: auto;
          white-space: pre-wrap;
          word-break: break-word;
        }
        .markdown-body.github-light pre code { background: transparent !important; white-space: pre-wrap; }
        .markdown-body.github-light blockquote {
          margin: 0 0 1em;
          padding: 8px 14px;
          border-left: 3px solid #0969da;
          color: #57606a !important;
          word-wrap: break-word;
        }
        .markdown-body.github-light blockquote.chapter-metadata {
          background-color: #f0f6fc !important;
          border-left-color: #0969da;
        }
        .markdown-body.github-light a { color: #0969da !important; word-break: break-all; }
        .markdown-body.github-light table { border-collapse: collapse; width: 100%; display: block; overflow-x: auto; }
        .markdown-body.github-light th, .markdown-body.github-light td {
          border: 1px solid #d8dee4;
          padding: 6px 10px;
          word-break: break-word;
        }
        .markdown-body.github-light img { max-width: 100%; height: auto; }
        """;
}
