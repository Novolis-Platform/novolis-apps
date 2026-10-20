namespace Novolis.Reach.Protocol.Host;

/// <summary>
/// Local lifecycle notification from the interactive session helper.
/// </summary>
public sealed record ReachSessionLifecycleStatus(
    string State,
    bool CaptureActive);
