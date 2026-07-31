namespace BooksMobile;

/// <summary>Keeps the device screen awake (e.g. while listening to a chapter).</summary>
public interface IScreenWakeLock
{
    /// <summary>Acquire a wake lock; dispose to release.</summary>
    IDisposable Acquire(string reason);
}

/// <summary>No-op wake lock for desktop / hosts without a screen policy.</summary>
public sealed class NullScreenWakeLock : IScreenWakeLock
{
    public IDisposable Acquire(string reason) => Empty.Instance;

    sealed class Empty : IDisposable
    {
        public static readonly Empty Instance = new();
        public void Dispose()
        {
        }
    }
}
