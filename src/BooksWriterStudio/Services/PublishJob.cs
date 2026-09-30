namespace BooksWriterStudio.Services;

internal sealed class PublishJob
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public PublishJobStatus Status { get; set; } = PublishJobStatus.Queued;
    public string? Detail { get; set; }
    public string? Log { get; set; }
    public string? OutputPath { get; set; }
    public double? Progress { get; set; }
    public string? ProgressLabel { get; set; }
    public List<PublishJobStepProgress> StepProgress { get; } = [];
    public CancellationTokenSource Cts { get; } = new();
}
