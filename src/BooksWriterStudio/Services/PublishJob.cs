namespace BooksWriterStudio.Services;

internal enum PublishJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

internal sealed class PublishJob
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public PublishJobStatus Status { get; set; } = PublishJobStatus.Queued;
    public string? Detail { get; set; }
    public string? Log { get; set; }
    public string? OutputPath { get; set; }
    public CancellationTokenSource Cts { get; } = new();
}

internal sealed class PublishJobQueue
{
    readonly List<PublishJob> _jobs = [];
    readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<PublishJob> Jobs
    {
        get
        {
            lock (_gate)
                return _jobs.ToList();
        }
    }

    public PublishJob Enqueue(string title, Func<PublishJob, CancellationToken, Task> work)
    {
        var job = new PublishJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
        };

        lock (_gate)
            _jobs.Insert(0, job);

        Changed?.Invoke();
        _ = RunJobAsync(job, work);
        return job;
    }

    public void Cancel(PublishJob job)
    {
        if (job.Status is PublishJobStatus.Queued or PublishJobStatus.Running)
            job.Cts.Cancel();
    }

    async Task RunJobAsync(PublishJob job, Func<PublishJob, CancellationToken, Task> work)
    {
        job.Status = PublishJobStatus.Running;
        Changed?.Invoke();

        try
        {
            await work(job, job.Cts.Token).ConfigureAwait(false);
            if (job.Cts.IsCancellationRequested)
            {
                job.Status = PublishJobStatus.Cancelled;
                job.Detail ??= "Cancelled";
            }
            else
            {
                job.Status = PublishJobStatus.Succeeded;
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = PublishJobStatus.Cancelled;
            job.Detail ??= "Cancelled";
        }
        catch (Exception ex)
        {
            job.Status = PublishJobStatus.Failed;
            job.Detail = ex.Message;
            job.Log = ex.ToString();
        }
        finally
        {
            Changed?.Invoke();
        }
    }
}
