namespace Novolis.Reach.Protocol.Host;

/// <summary>
/// Runtime state for the interactive user session owned by a Reach device.
/// </summary>
public sealed record ReachUserSessionStatus(
    uint? SessionId,
    string? DomainName,
    string? UserName,
    string State,
    bool HelperConnected,
    string CaptureState);
