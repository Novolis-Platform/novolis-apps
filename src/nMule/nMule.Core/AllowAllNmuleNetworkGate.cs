using System.Net;

namespace Novolis.NMule.Core;

/// <summary>
/// Permissive fallback used by headless tooling and tests. Android composition
/// always supplies the fail-closed gate.
/// </summary>
public sealed class AllowAllNmuleNetworkGate : INmuleNetworkGate
{
    /// <inheritdoc />
    public bool SupportsTailscaleOverride => false;

    /// <inheritdoc />
    public bool AllowTailscale
    {
        get => false;
        set
        {
        }
    }

    /// <inheritdoc />
    public ValueTask<NmuleNetworkDecision> CheckAsync(
        IPAddress? endpointAddress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NmuleNetworkDecision.Allow());
    }
}
