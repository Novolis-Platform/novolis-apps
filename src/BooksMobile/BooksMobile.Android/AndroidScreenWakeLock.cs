using Android.Views;
using BooksMobile;

namespace BooksMobile.Android;

/// <summary>Keeps the Android activity window awake via <see cref="WindowManagerFlags.KeepScreenOn"/>.</summary>
public sealed class AndroidScreenWakeLock : IScreenWakeLock
{
    public IDisposable Acquire(string reason)
    {
        var activity = MainActivity.Current;
        if (activity?.Window is null)
            return NullScreenWakeLockEmpty.Instance;

        activity.RunOnUiThread(() =>
            activity.Window?.AddFlags(WindowManagerFlags.KeepScreenOn));
        return new Releaser(activity);
    }

    sealed class Releaser : IDisposable
    {
        readonly MainActivity _activity;
        int _disposed;

        public Releaser(MainActivity activity) => _activity = activity;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _activity.RunOnUiThread(() =>
                _activity.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn));
        }
    }

    sealed class NullScreenWakeLockEmpty : IDisposable
    {
        public static readonly NullScreenWakeLockEmpty Instance = new();
        public void Dispose()
        {
        }
    }
}
