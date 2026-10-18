using System.Net;
using System.Net.NetworkInformation;
using Novolis.NMule.Core;

namespace Novolis.NMule;

/// <summary>
/// Windows VPN admission gate. It recognizes active tunnel/PPP adapters and
/// common tunnel driver names, then applies the shared Tailscale policy.
/// </summary>
public sealed class WindowsNmuleNetworkGate : INmuleNetworkGate
{
    private const string TailscaleDnsAddress = "100.100.100.100";
    private int _allowTailscale;
    private int _allowUnrestrictedNetwork;

    /// <inheritdoc />
    public bool SupportsTailscaleOverride => true;

    /// <inheritdoc />
    public bool AllowTailscale
    {
        get => Volatile.Read(ref _allowTailscale) != 0;
        set => Volatile.Write(ref _allowTailscale, value ? 1 : 0);
    }

    /// <inheritdoc />
    public bool SupportsUnrestrictedNetworkOverride => true;

    /// <inheritdoc />
    public bool AllowUnrestrictedNetwork
    {
        get => Volatile.Read(ref _allowUnrestrictedNetwork) != 0;
        set => Volatile.Write(ref _allowUnrestrictedNetwork, value ? 1 : 0);
    }

    /// <inheritdoc />
    public ValueTask<NmuleNetworkDecision> CheckAsync(
        IPAddress? endpointAddress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (AllowUnrestrictedNetwork)
            return ValueTask.FromResult(NmuleNetworkDecision.Allow());

        try
        {
            var vpnInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsActive)
                .Where(IsVpnInterface)
                .ToArray();
            if (vpnInterfaces.Length == 0)
            {
                return ValueTask.FromResult(
                    NmuleNetworkPolicy.Evaluate(
                        NmuleVpnTransportKind.NoVpn,
                        AllowTailscale));
            }

            var tailscale = endpointAddress is not null
                && IsTailscaleIpv4(endpointAddress);
            if (!tailscale)
            {
                foreach (var networkInterface in vpnInterfaces)
                {
                    if (IsTailscaleInterface(networkInterface))
                    {
                        tailscale = true;
                        break;
                    }
                }
            }

            return ValueTask.FromResult(
                NmuleNetworkPolicy.Evaluate(
                    tailscale
                        ? NmuleVpnTransportKind.Tailscale
                        : NmuleVpnTransportKind.Vpn,
                    AllowTailscale));
        }
        catch (Exception)
        {
            return ValueTask.FromResult(
                NmuleNetworkDecision.Deny(
                    "Windows could not verify the active VPN and remains blocked."));
        }
    }

    private static bool IsActive(NetworkInterface networkInterface) =>
        networkInterface.OperationalStatus == OperationalStatus.Up;

    private static bool IsVpnInterface(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType
            is NetworkInterfaceType.Tunnel
            or NetworkInterfaceType.Ppp)
        {
            return true;
        }

        var identity = $"{networkInterface.Name} {networkInterface.Description}";
        return Contains(identity, "vpn")
            || Contains(identity, "wireguard")
            || Contains(identity, "wintun")
            || Contains(identity, "openvpn")
            || Contains(identity, "tailscale");
    }

    private static bool IsTailscaleInterface(NetworkInterface networkInterface)
    {
        var identity = $"{networkInterface.Name} {networkInterface.Description}";
        if (Contains(identity, "tailscale"))
            return true;

        var properties = networkInterface.GetIPProperties();
        if (properties.DnsAddresses.Any(IsTailscaleDns))
            return true;

        return properties.UnicastAddresses
            .Any(address => IsTailscaleIpv4(address.Address));
    }

    private static bool IsTailscaleDns(IPAddress address) =>
        address.ToString().Equals(
            TailscaleDnsAddress,
            StringComparison.Ordinal);

    private static bool IsTailscaleIpv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }

    private static bool Contains(string value, string part) =>
        value.Contains(part, StringComparison.OrdinalIgnoreCase);
}
