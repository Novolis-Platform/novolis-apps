using Markdig;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ManuscriptStudio.Extensions.BookAuthoring.Content;

namespace ManuscriptStudio.Extensions.BookAuthoring.Rendering;

internal static class BookPdfExporter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static void Export(BookInfo book, string outputPdfPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var combined = ConcatenateChapters(book);
        var plainSections = ExtractPlainSections(combined);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(6f, 9f, Unit.Inch);
                page.MarginTop(0.75f, Unit.Inch);
                page.MarginBottom(0.75f, Unit.Inch);
                page.MarginHorizontal(0.65f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Georgia));

                page.Header().AlignCenter().Text(book.Title).SemiBold().FontSize(9);
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text(book.Title).FontSize(20).SemiBold();
                    if (!string.IsNullOrWhiteSpace(book.Subtitle))
                        col.Item().Text(book.Subtitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(book.Author))
                        col.Item().PaddingTop(12).Text(book.Author).FontSize(11);
                });
            });

            container.Page(page =>
            {
                page.Size(6f, 9f, Unit.Inch);
                page.MarginTop(0.75f, Unit.Inch);
                page.MarginBottom(0.75f, Unit.Inch);
                page.MarginHorizontal(0.65f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontSize(11).LineHeight(1.35f).FontFamily(Fonts.Georgia));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    foreach (var section in plainSections)
                    {
                        if (section.IsHeading)
                            col.Item().PaddingTop(8).Text(section.Text).FontSize(14).SemiBold();
                        else
                            col.Item().Text(section.Text);
                    }
                });
            });
        }).GeneratePdf(outputPdfPath);
    }

    private static string ConcatenateChapters(BookInfo book)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var chapter in book.Chapters)
        {
            var body = File.ReadAllText(chapter.FilePath);
            if (body.StartsWith('\uFEFF'))
                body = body[1..];
            sb.AppendLine(body.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<PlainSection> ExtractPlainSections(string markdown)
    {
        var doc = Markdown.Parse(markdown, Pipeline);
        var sections = new List<PlainSection>();
        foreach (var block in doc)
        {
            if (block is Markdig.Syntax.HeadingBlock heading)
            {
                var text = heading.Inline?.FirstChild is Markdig.Syntax.Inlines.LiteralInline lit
                    ? lit.Content.ToString()
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    sections.Add(new PlainSection(text, true));
            }
            else if (block is Markdig.Syntax.ParagraphBlock para)
            {
                var text = InlineToPlain(para.Inline);
                if (!string.IsNullOrWhiteSpace(text))
                    sections.Add(new PlainSection(text, false));
            }
        }

        return sections;
    }

    private static string InlineToPlain(Markdig.Syntax.Inlines.ContainerInline? inline)
    {
        if (inline is null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var child in inline)
        {
            if (child is Markdig.Syntax.Inlines.LiteralInline lit)
                sb.Append(lit.Content);
            else if (child is Markdig.Syntax.Inlines.LineBreakInline)
                sb.Append(' ');
        }

        return sb.ToString().Trim();
    }

    private sealed record PlainSection(string Text, bool IsHeading);
}
