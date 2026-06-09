namespace ManuscriptStudio.Extensions.BookAuthoring.Helpers;

internal static class DialogueInsertHelper
{
    public static string InsertDialogueBlock(string text, int caretIndex) =>
        InsertBlock(text, caretIndex, "\"First line of dialogue.\"\n\n\"Response from another speaker.\"");

    public static string InsertThinkingBlock(string text, int caretIndex) =>
        InsertBlock(text, caretIndex, "Continuous interior prose without line breaks between related thoughts in the same beat.");

    private static string InsertBlock(string text, int caretIndex, string block)
    {
        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        var prefix = caretIndex > 0 && text[caretIndex - 1] != '\n' ? "\n\n" : string.Empty;
        return text[..caretIndex] + prefix + block + "\n\n" + text[caretIndex..];
    }
}
