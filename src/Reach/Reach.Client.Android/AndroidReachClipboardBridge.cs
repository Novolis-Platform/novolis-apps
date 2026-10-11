using Android.App;
using Android.Content;
using Novolis.Reach.Client;

namespace Novolis.Reach.Client.Android;

/// <summary>Lifecycle-aware Android clipboard bridge for the Reach client.</summary>
internal sealed class AndroidReachClipboardBridge : IReachClipboardBridge
{
    private readonly Context _context = Application.Context;
    private ClipboardManager? _clipboard;
    private int _started;

    public event Action? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return Task.CompletedTask;

        _clipboard = (ClipboardManager?)_context.GetSystemService(
            Context.ClipboardService);
        if (_clipboard is null)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                "The Android Reach client has no clipboard service.");
        }

        _clipboard.PrimaryClipChanged += OnPrimaryClipChanged;
        return Task.CompletedTask;
    }

    public Task<string?> ReadTextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadTextCore());
    }

    public Task WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = _clipboard
            ?? throw new InvalidOperationException(
                "The Android Reach clipboard bridge is not started.");
        clipboard.PrimaryClip = ClipData.NewPlainText("Reach", text);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return Task.CompletedTask;

        if (_clipboard is { } clipboard)
            clipboard.PrimaryClipChanged -= OnPrimaryClipChanged;
        _clipboard = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() =>
        await StopAsync().ConfigureAwait(false);

    private string? ReadTextCore()
    {
        var clip = _clipboard?.PrimaryClip;
        if (clip is null || clip.ItemCount == 0)
            return null;

        return clip.GetItemAt(0)?.CoerceToText(_context)?.ToString();
    }

    private void OnPrimaryClipChanged(object? sender, EventArgs args) =>
        Changed?.Invoke();
}
