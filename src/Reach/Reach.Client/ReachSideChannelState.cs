namespace Novolis.Reach.Client;

/// <summary>Lifecycle state of the optional Reach side channel.</summary>
public enum ReachSideChannelState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Degraded,
}
