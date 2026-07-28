namespace BooksWriterStudio.Services;

internal sealed class WriterSettings
{
    public string? ContentRoot { get; set; }
    public string? LastSeriesId { get; set; }
    public string? LastBookId { get; set; }
    public string? LastChapterId { get; set; }
    public double NavColumnWidth { get; set; } = 260;
    public double ContextColumnWidth { get; set; } = 420;
    public double EditorFontSize { get; set; } = 14;
    public double EditorZoom { get; set; } = 1.0;
    public string Theme { get; set; } = "Dark";
    public string? CustomDictionaryPath { get; set; }
}
