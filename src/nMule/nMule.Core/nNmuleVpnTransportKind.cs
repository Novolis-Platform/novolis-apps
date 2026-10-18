namespace Novolis.NMule.Core;

/// <summary>Classifies the network path observed by the nMule host.</summary>
public enum NmuleVpnTransportKind
{
    NoVpn,
    Vpn,
    Tailscale,
}
