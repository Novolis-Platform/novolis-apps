using ReadAloud.Services;

namespace ReadAloud.Unit;

public sealed class MarkdownSpeechPreParserTests
{
    [Test]
    public async Task Normalize_removes_markup_but_keeps_readable_content()
    {
        var markdown = """
            ---
            title: Example
            ---

            # Welcome to **Read Aloud**

            Read [this guide](https://example.com/docs), not the raw URL
            This is a soft line break.

            This starts a new paragraph.

            - [x] Finished item
            - [ ] Open item

            > [!NOTE] This is useful context.

            | Name | Value |
            | --- | --- |
            | Alpha | Bravo |

            ```mermaid
            flowchart LR
            A --> B
            ```
            """;

        var normalized = MarkdownSpeechPreParser.Normalize(markdown);

        await Assert.That(normalized).Contains("Welcome to Read Aloud.");
        await Assert.That(normalized).Contains("Read this guide, not the raw URL,");
        await Assert.That(normalized).Contains("raw URL, This is a soft line break.");
        await Assert.That(normalized).Contains("soft line break. This starts a new paragraph.");
        await Assert.That(normalized).Contains("Completed list item. Finished item");
        await Assert.That(normalized).Contains("Todo list item. Open item");
        await Assert.That(normalized).Contains("Note. This is useful context.");
        await Assert.That(normalized).Contains("Table row. Name: Alpha. Value: Bravo");
        await Assert.That(normalized).Contains("Diagram omitted.");
        await Assert.That(normalized).DoesNotContain("https://example.com");
        await Assert.That(normalized).DoesNotContain("**");
        await Assert.That(normalized).DoesNotContain("```");
        await Assert.That(normalized).DoesNotContain("flowchart");
    }
}
