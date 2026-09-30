namespace ReadAloud;

/// <summary>Keeps the device screen awake while listening.</summary>
public interface IScreenWakeLock
{
    /// <summary>Acquire a wake lock; dispose to release.</summary>
    IDisposable Acquire(string reason);
}
