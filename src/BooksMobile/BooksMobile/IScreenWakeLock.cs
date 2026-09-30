namespace BooksMobile;

/// <summary>Keeps the device screen awake (e.g. while listening to a chapter).</summary>
public interface IScreenWakeLock
{
    /// <summary>Acquire a wake lock; dispose to release.</summary>
    IDisposable Acquire(string reason);
}
