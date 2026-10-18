using System.Net;

namespace Novolis.NMule.Core;

/// <summary>Guards every nMule operation that can create network traffic.</summary>
public interface INmuleNetworkGate
{
    /// <summary>Gets whether the host exposes a Tailscale override control.</summary>
    bool SupportsTailscaleOverride { get; }

    /// <summary>Gets or sets the explicit, session-scoped Tailscale override.</summary>
    bool AllowTailscale { get; set; }

    /// <summary>Gets whether the host exposes the complete VPN override.</summary>
    bool SupportsUnrestrictedNetworkOverride { get; }

    /// <summary>
    /// Gets or sets the session-scoped override that permits network activity
    /// without an active VPN. This is off by default.
    /// </summary>
    bool AllowUnrestrictedNetwork { get; set; }

    /// <summary>
    /// Checks the currently active path before network I/O. The optional address
    /// is used for conservative Tailscale endpoint classification.
    /// </summary>
    ValueTask<NmuleNetworkDecision> CheckAsync(
        IPAddress? endpointAddress = null,
        CancellationToken cancellationToken = default);
}
