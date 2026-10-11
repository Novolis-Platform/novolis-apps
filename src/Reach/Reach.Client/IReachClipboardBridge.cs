namespace Novolis.Reach.Client;

/// <summary>Platform clipboard access used by Reach clipboard synchronization.</summary>
public interface IReachClipboardBridge : IAsyncDisposable
{
    /// <summary>Raised when the local text clipboard changes.</summary>
    event Action? Changed;

    /// <summary>Starts observing the local clipboard.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the current local text clipboard.</summary>
    Task<string?> ReadTextAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes text to the local clipboard.</summary>
    Task WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>Stops observing the local clipboard.</summary>
    Task StopAsync();
}
