using System.Net;
using Android.Net;
using Novolis.NMule.Core;

namespace Novolis.NMule;

/// <summary>
/// Android VPN admission gate. It intentionally fails closed when Android
/// cannot provide a trustworthy active-network snapshot.
/// </summary>
public sealed class AndroidNmuleNetworkGate : INmuleNetworkGate
{
    private const string TailscaleDnsAddress = "100.100.100.100";
    private readonly ConnectivityManager _connectivity;
    private int _allowTailscale;
    private int _allowUnrestrictedNetwork;

    /// <summary>Creates a gate over the process' active Android network.</summary>
    public AndroidNmuleNetworkGate()
    {
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException(
                "Android application context is unavailable.");
        _connectivity = context.GetSystemService(
                global::Android.Content.Context.ConnectivityService)
            as ConnectivityManager
            ?? throw new InvalidOperationException(
                "Android ConnectivityManager is unavailable.");
    }

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
        {
            return ValueTask.FromResult(NmuleNetworkDecision.Allow());
        }

        try
        {
            var activeNetwork = _connectivity.ActiveNetwork;
            if (activeNetwork is null)
            {
                return ValueTask.FromResult(
                    NmuleNetworkPolicy.Evaluate(
                        NmuleVpnTransportKind.NoVpn,
                        AllowTailscale,
                        AllowUnrestrictedNetwork));
            }

            var capabilities = _connectivity.GetNetworkCapabilities(activeNetwork);
            if (capabilities is null
                || !capabilities.HasTransport(
                    TransportType.Vpn))
            {
                return ValueTask.FromResult(
                    NmuleNetworkPolicy.Evaluate(
                        NmuleVpnTransportKind.NoVpn,
                        AllowTailscale,
                        AllowUnrestrictedNetwork));
            }

            var linkProperties = _connectivity.GetLinkProperties(activeNetwork);
            var transport = IsTailscale(
                    endpointAddress,
                    linkProperties)
                ? NmuleVpnTransportKind.Tailscale
                : NmuleVpnTransportKind.Vpn;
            return ValueTask.FromResult(
                NmuleNetworkPolicy.Evaluate(
                    transport,
                    AllowTailscale,
                    AllowUnrestrictedNetwork));
        }
        catch (global::Java.Lang.SecurityException)
        {
            return ValueTask.FromResult(
                NmuleNetworkDecision.Deny(
                    "Android did not allow nMule to verify the active VPN."));
        }
        catch (Exception)
        {
            return ValueTask.FromResult(
                NmuleNetworkDecision.Deny(
                    "nMule could not verify the active VPN and remains blocked."));
        }
    }

    private static bool IsTailscale(
        IPAddress? endpointAddress,
        LinkProperties? linkProperties)
    {
        if (endpointAddress is not null
            && IsTailscaleIpv4(endpointAddress))
        {
            return true;
        }

        var interfaceName = linkProperties?.InterfaceName;
        if (interfaceName?.Contains(
                "tailscale",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (linkProperties?.LinkAddresses is { } linkAddresses)
        {
            foreach (var linkAddress in linkAddresses)
            {
                if (linkAddress?.Address?.HostAddress is { } hostAddress
                    && IPAddress.TryParse(hostAddress, out var address)
                    && IsTailscaleIpv4(address))
                {
                    return true;
                }
            }
        }

        if (linkProperties?.DnsServers is not { } dnsServers)
            return false;

        foreach (var dnsServer in dnsServers)
        {
            if (string.Equals(
                    dnsServer?.HostAddress,
                    TailscaleDnsAddress,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTailscaleIpv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }
}
