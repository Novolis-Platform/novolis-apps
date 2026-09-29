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
        await Assert.That(normalized).Contains("Table cannot be read.");
        await Assert.That(normalized).Contains("Code-block cannot be read.");
        await Assert.That(normalized).DoesNotContain("Alpha");
        await Assert.That(normalized).DoesNotContain("Bravo");
        await Assert.That(normalized).DoesNotContain("https://example.com");
        await Assert.That(normalized).DoesNotContain("**");
        await Assert.That(normalized).DoesNotContain("```");
        await Assert.That(normalized).DoesNotContain("flowchart");
    }

    [Test]
    public async Task Normalize_replaces_every_fenced_code_variant_without_leaking_code()
    {
        var markdown = """
            ```
            plain code
            ```

            ```plaintext
            plaintext code
            ```

            ```text
            text code
            ```

            ```mermaid
            flowchart LR
            A --> B
            ```

            ~~~
            tilde code
            ~~~
            """;

        var normalized = MarkdownSpeechPreParser.Normalize(markdown);

        await Assert.That(normalized)
            .Contains("Code-block cannot be read.");
        await Assert.That(
                normalized.Split("Code-block cannot be read.", StringSplitOptions.None).Length - 1)
            .IsEqualTo(5);
        await Assert.That(normalized).DoesNotContain("plain code");
        await Assert.That(normalized).DoesNotContain("plaintext code");
        await Assert.That(normalized).DoesNotContain("text code");
        await Assert.That(normalized).DoesNotContain("flowchart");
        await Assert.That(normalized).DoesNotContain("tilde code");
    }
}
