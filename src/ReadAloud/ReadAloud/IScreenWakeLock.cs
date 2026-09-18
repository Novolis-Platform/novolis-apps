namespace ReadAloud;

/// <summary>Keeps the device screen awake while listening.</summary>
public interface IScreenWakeLock
{
    /// <summary>Acquire a wake lock; dispose to release.</summary>
    IDisposable Acquire(string reason);
}

/// <summary>No-op wake lock for hosts without a screen policy.</summary>
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
