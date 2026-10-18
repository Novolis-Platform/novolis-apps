namespace Novolis.NMule.Core;

/// <summary>Raised when the active path does not satisfy nMule's network policy.</summary>
public sealed class NmuleNetworkBlockedException : InvalidOperationException
{
    /// <summary>Creates an exception with the policy's user-facing reason.</summary>
    public NmuleNetworkBlockedException(string reason)
        : base(reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    }
}
