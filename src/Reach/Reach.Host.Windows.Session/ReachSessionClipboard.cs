using System.Collections.Concurrent;
using Novolis.Windows.Clipboard;

namespace Novolis.Reach.Host.Windows.Session;

/// <summary>
/// Serializes Windows clipboard calls on a dedicated STA thread and observes
/// text changes for the interactive session helper.
/// </summary>
internal sealed class ReachSessionClipboard : IAsyncDisposable
{
    private sealed class WorkItem(
        Action action,
        TaskCompletionSource<object?> completion)
    {
        internal Action Action { get; } = action;
        internal TaskCompletionSource<object?> Completion { get; } = completion;
    }

    private readonly WindowsClipboardService _service;
    private readonly BlockingCollection<WorkItem> _work = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly Thread _thread;
    private string? _lastText;
    private int _disposed;

    internal ReachSessionClipboard(WindowsClipboardService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Novolis Reach clipboard",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    internal event Action<string?>? TextChanged;

    internal Task<string?> ReadTextAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            () => _service.ReadText(),
            cancellationToken);

    internal Task<IReadOnlyList<string>> ReadFileDropListAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            () => (IReadOnlyList<string>)_service.ReadFileDropList().ToArray(),
            cancellationToken);

    internal Task WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            () =>
            {
                _service.WriteText(text);
                _lastText = text;
            },
            cancellationToken);

    internal Task WriteFileDropListAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var paths = files.ToArray();
        return InvokeAsync(
            () =>
            {
                _service.WriteFileDropList(paths);
                _lastText = _service.ReadText();
            },
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        _stop.Set();
        _work.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _work.Dispose();
        _ready.Dispose();
        _stop.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<T> InvokeAsync<T>(
        Func<T> work,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (!_work.TryAdd(
                    new WorkItem(
                        () => completion.TrySetResult(work()),
                        completion),
                    Timeout.Infinite,
                    cancellationToken))
            {
                return Task.FromException<T>(
                    new InvalidOperationException(
                        "The Reach clipboard worker is stopping."));
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        return AwaitResultAsync<T>(completion.Task);
    }

    private Task InvokeAsync(
        Action work,
        CancellationToken cancellationToken) =>
        InvokeAsync(
            () =>
            {
                work();
                return (object?)null;
            },
            cancellationToken);

    private static async Task<T> AwaitResultAsync<T>(
        Task<object?> completion)
    {
        var result = await completion.ConfigureAwait(false);
        return result is null ? default! : (T)result;
    }

    private void Run()
    {
        try
        {
            try
            {
                _lastText = _service.ReadText();
            }
            catch
            {
                _lastText = null;
            }
        }
        finally
        {
            _ready.Set();
        }

        while (!_stop.IsSet)
        {
            if (_work.TryTake(out var item, 250))
            {
                Execute(item);
                continue;
            }

            Poll();
        }

        while (_work.TryTake(out var item))
            Execute(item);
    }

    private void Execute(WorkItem item)
    {
        try
        {
            item.Action();
        }
        catch (Exception exception)
        {
            item.Completion.TrySetException(exception);
        }
    }

    private void Poll()
    {
        string? current;
        try
        {
            current = _service.ReadText();
        }
        catch
        {
            return;
        }

        if (string.Equals(current, _lastText, StringComparison.Ordinal))
            return;

        _lastText = current;
        try
        {
            TextChanged?.Invoke(current);
        }
        catch
        {
            // A notification subscriber must not terminate the STA worker.
        }
    }
}
