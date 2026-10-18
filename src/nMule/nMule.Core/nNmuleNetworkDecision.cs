namespace Novolis.NMule.Core;

/// <summary>Result of evaluating whether nMule may perform network I/O.</summary>
public readonly record struct NmuleNetworkDecision(
    bool IsAllowed,
    string? DenialReason)
{
    /// <summary>Creates an allowed decision.</summary>
    public static NmuleNetworkDecision Allow() => new(true, null);

    /// <summary>Creates a denied decision with a user-facing reason.</summary>
    public static NmuleNetworkDecision Deny(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, reason);
    }
}
