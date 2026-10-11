using Avalonia.Input;

namespace Novolis.Avalonia.Reach;

internal sealed class ReachPressedKeyTracker
{
    private readonly HashSet<Key> _keys = [];
    private readonly object _gate = new();

    internal bool TryPress(Key key)
    {
        lock (_gate)
        {
            return _keys.Add(key);
        }
    }

    internal bool TryRelease(Key key)
    {
        lock (_gate)
        {
            return _keys.Remove(key);
        }
    }

    internal Key[] ReleaseAll()
    {
        lock (_gate)
        {
            var pressed = _keys.ToArray();
            _keys.Clear();
            return pressed;
        }
    }
}
