namespace ManuscriptStudio.Extensions.BookAuthoring.Helpers;

internal static class MetadataInsertHelper
{
    public static string InsertAtCursor(string text, int caretIndex, string tag, string placeholder = "")
    {
        var line = $"> [!{tag}] {placeholder}".TrimEnd();
        return InsertLine(text, caretIndex, line);
    }

    public static string InsertChapterTag(string text, int caretIndex, double chapterNumber) =>
        InsertLine(text, caretIndex, $"<!-- booktools-chapter: {chapterNumber} -->");

    private static string InsertLine(string text, int caretIndex, string line)
    {
        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        var prefix = caretIndex > 0 && text[caretIndex - 1] != '\n' ? Environment.NewLine : string.Empty;
        var suffix = caretIndex < text.Length && text[caretIndex] != '\n' ? Environment.NewLine : string.Empty;
        return text[..caretIndex] + prefix + line + suffix + text[caretIndex..];
    }
}
