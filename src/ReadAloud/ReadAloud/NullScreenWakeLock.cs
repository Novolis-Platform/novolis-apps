namespace ReadAloud;

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
