namespace Novolis.NMule.Core;

/// <summary>Applies nMule's fail-closed VPN policy without platform APIs.</summary>
public static class NmuleNetworkPolicy
{
    /// <summary>Evaluates one observed transport state.</summary>
    public static NmuleNetworkDecision Evaluate(
        NmuleVpnTransportKind transport,
        bool allowTailscale,
        bool allowUnrestrictedNetwork = false)
    {
        if (allowUnrestrictedNetwork)
            return NmuleNetworkDecision.Allow();

        return transport switch
        {
            NmuleVpnTransportKind.Vpn => NmuleNetworkDecision.Allow(),
            NmuleVpnTransportKind.Tailscale when allowTailscale =>
                NmuleNetworkDecision.Allow(),
            NmuleVpnTransportKind.Tailscale =>
                NmuleNetworkDecision.Deny(
                    "Tailscale is blocked by default. Enable the explicit Tailscale override to continue."),
            _ => NmuleNetworkDecision.Deny(
                "nMule requires an active VPN before it can use eD2K or Kad."),
        };
    }
}
