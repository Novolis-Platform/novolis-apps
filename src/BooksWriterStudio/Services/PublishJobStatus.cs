namespace BooksWriterStudio.Services;

internal enum PublishJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}
