using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Novolis.Reach.Client;

namespace Novolis.Avalonia.Reach;

/// <summary>
/// Observes the Avalonia clipboard with a small poll because Avalonia does not
/// expose a portable clipboard-change event.
/// </summary>
internal sealed class AvaloniaReachClipboardBridge : IReachClipboardBridge
{
    private readonly Control _owner;
    private readonly DispatcherTimer _pollTimer;
    private string? _lastText;
    private int _started;

    internal AvaloniaReachClipboardBridge(Control owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _pollTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(750),
            DispatcherPriority.Background,
            (_, _) => _ = PollAsync());
    }

    public event Action? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        RunOnUiThreadAsync(
            () => StartCoreAsync(cancellationToken));

    public Task<string?> ReadTextAsync(
        CancellationToken cancellationToken = default) =>
        RunOnUiThreadAsync(
            () => ReadTextCoreAsync(cancellationToken));

    public Task WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        RunOnUiThreadAsync(
            () => WriteTextCoreAsync(text, cancellationToken));

    public Task StopAsync() =>
        RunOnUiThreadAsync(
            () =>
            {
                Interlocked.Exchange(ref _started, 0);
                _pollTimer.Stop();
                return Task.CompletedTask;
            });

    public async ValueTask DisposeAsync() =>
        await StopAsync().ConfigureAwait(false);

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        try
        {
            _lastText = await ReadTextCoreAsync(cancellationToken)
                .ConfigureAwait(true);
            _pollTimer.Start();
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }

    private async Task<string?> ReadTextCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = TopLevel.GetTopLevel(_owner)?.Clipboard;
        return clipboard is null
            ? null
            : await clipboard.TryGetTextAsync().ConfigureAwait(true);
    }

    private async Task WriteTextCoreAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = TopLevel.GetTopLevel(_owner)?.Clipboard
            ?? throw new InvalidOperationException(
                "The Reach client window has no local clipboard.");
        await clipboard.SetTextAsync(text).ConfigureAwait(true);
        _lastText = text;
    }

    private async Task PollAsync()
    {
        if (Volatile.Read(ref _started) == 0)
            return;

        try
        {
            var text = await ReadTextCoreAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (string.Equals(text, _lastText, StringComparison.Ordinal))
                return;

            _lastText = text;
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // Clipboard providers can be temporarily unavailable while another
            // process owns the native clipboard. The next poll retries.
        }
    }

    private static Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return action();

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(
            async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                    completion.TrySetResult(null);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
        return completion.Task;
    }

    private static Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return action();

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(
            async () =>
            {
                try
                {
                    completion.TrySetResult(
                        await action().ConfigureAwait(true));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
        return completion.Task;
    }
}
