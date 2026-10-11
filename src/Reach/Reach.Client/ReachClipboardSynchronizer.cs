namespace Novolis.Reach.Client;

/// <summary>Synchronizes text between a Reach session and a local clipboard.</summary>
public sealed class ReachClipboardSynchronizer : IAsyncDisposable
{
    private readonly ReachClientSession _session;
    private readonly IReachClipboardBridge _bridge;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _started;
    private int _disposed;
    private string? _lastPublishedText;
    private string? _lastRemoteText;

    /// <summary>Creates a clipboard synchronizer for one Reach session.</summary>
    public ReachClipboardSynchronizer(
        ReachClientSession session,
        IReachClipboardBridge bridge)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _bridge.Changed += OnLocalClipboardChanged;
        _session.ClipboardContentReceived += OnRemoteClipboardContent;
    }

    /// <summary>Raised for user-facing clipboard synchronization status.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Starts local clipboard observation.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        try
        {
            await _bridge.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Volatile.Write(ref _started, 0);
            throw;
        }
    }

    /// <summary>Requests the host clipboard after a session is ready.</summary>
    public async Task RequestRemoteClipboardAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanSynchronize())
            return;

        try
        {
            await _session.SendAsync(
                    ReachMessageType.ClipboardChanged,
                    new ReachClipboardChanged("text"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or ObjectDisposedException)
        {
            StatusChanged?.Invoke($"Clipboard request failed: {exception.Message}");
        }
    }

    /// <summary>Stops local clipboard observation.</summary>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return;

        await _bridge.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _bridge.Changed -= OnLocalClipboardChanged;
        _session.ClipboardContentReceived -= OnRemoteClipboardContent;
        await StopAsync().ConfigureAwait(false);
        await _operationGate.WaitAsync().ConfigureAwait(false);
        _operationGate.Release();
        await _bridge.DisposeAsync().ConfigureAwait(false);
    }

    private bool CanSynchronize() =>
        Volatile.Read(ref _disposed) == 0
        &&
        Volatile.Read(ref _started) != 0
        && _session.IsConnected
        && _session.NegotiatedCapabilities?.Supports(ReachCapability.ClipboardText)
            == true;

    private void OnLocalClipboardChanged() =>
        _ = PublishLocalClipboardAsync();

    private void OnRemoteClipboardContent(ReachClipboardContent content) =>
        _ = ApplyRemoteClipboardAsync(content);

    private async Task PublishLocalClipboardAsync()
    {
        if (!CanSynchronize())
            return;

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!CanSynchronize())
                return;

            var text = await _bridge.ReadTextAsync().ConfigureAwait(false) ?? string.Empty;
            if (string.Equals(
                    Interlocked.Exchange(ref _lastRemoteText, null),
                    text,
                    StringComparison.Ordinal)
                || string.Equals(
                    Volatile.Read(ref _lastPublishedText),
                    text,
                    StringComparison.Ordinal))
            {
                return;
            }

            await _session.SendClipboardTextAsync(text).ConfigureAwait(false);
            Interlocked.Exchange(ref _lastPublishedText, text);
            StatusChanged?.Invoke("Local clipboard sent to the remote session.");
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or ObjectDisposedException)
        {
            StatusChanged?.Invoke($"Clipboard send failed: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task ApplyRemoteClipboardAsync(ReachClipboardContent content)
    {
        if (!string.Equals(content.Format, "text", StringComparison.OrdinalIgnoreCase)
            || content.Text is null)
        {
            if (string.Equals(
                    content.Format,
                    "files",
                    StringComparison.OrdinalIgnoreCase))
            {
                StatusChanged?.Invoke(
                    "Remote file clipboard content is not enabled yet.");
            }

            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            Interlocked.Exchange(ref _lastRemoteText, content.Text);
            await _bridge.WriteTextAsync(content.Text).ConfigureAwait(false);
            Interlocked.Exchange(ref _lastPublishedText, content.Text);
            StatusChanged?.Invoke("Remote clipboard copied locally.");
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or ObjectDisposedException)
        {
            StatusChanged?.Invoke($"Clipboard receive failed: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
