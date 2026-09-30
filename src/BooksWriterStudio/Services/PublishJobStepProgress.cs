namespace BooksWriterStudio.Services;

internal sealed class PublishJobStepProgress
{
    public required string Label { get; init; }
    public double Progress { get; set; }
    public string? StatusLabel { get; set; }
}
